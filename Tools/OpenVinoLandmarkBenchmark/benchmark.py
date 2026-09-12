from __future__ import annotations

import argparse
import hashlib
import importlib.metadata
import json
import math
import platform
import statistics
import struct
import sys
import tempfile
import time
import traceback
import zipfile
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence

VERSION = "1.0.0"
OV_PIN = "2026.3.0"
BUNDLE_REL = Path("Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes")
ENTRY = "pose_landmarks_detector.tflite"
BUNDLE_SIZE = 5_777_746
BUNDLE_SHA = "59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a"
MODEL_SIZE = 2_818_390
MODEL_SHA = "ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58"
INPUT_SHAPE = (1, 256, 256, 3)
OUTPUT_SHAPES = ((1,195),(1,1),(1,256,256,1),(1,64,64,39),(1,117))
SMALL_SHAPES = {(1,195),(1,1),(1,117)}
SENTIS_MEAN_MS = 46.24
SENTIS_RATE = 21.63

class BenchError(RuntimeError): pass

def sha_bytes(b: bytes) -> str: return hashlib.sha256(b).hexdigest()
def sha_file(p: Path) -> str:
    h = hashlib.sha256()
    with p.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""): h.update(chunk)
    return h.hexdigest()

def fail_identity(label: str, size: int, sha: str, exp_size: int, exp_sha: str) -> None:
    if size != exp_size or sha.lower() != exp_sha.lower():
        raise BenchError(
            f"{label} identity mismatch: expected size={exp_size} sha256={exp_sha}; "
            f"actual size={size} sha256={sha}. FAIL CLOSED; no substitute/download/conversion attempted."
        )

def extract_exact(bundle: Path, out_dir: Path) -> tuple[Path, dict[str, Any]]:
    if not bundle.is_file(): raise BenchError(f"Production bundle not found: {bundle}")
    bsize, bsha = bundle.stat().st_size, sha_file(bundle)
    fail_identity("bundle", bsize, bsha, BUNDLE_SIZE, BUNDLE_SHA)
    try:
        with zipfile.ZipFile(bundle) as z:
            hits = [i for i in z.infolist() if Path(i.filename).name == ENTRY]
            if len(hits) != 1: raise BenchError(f"Expected exactly one {ENTRY}; found {len(hits)}")
            data = z.read(hits[0])
    except zipfile.BadZipFile as e:
        raise BenchError(f"Production bundle is not a readable ZIP: {e}") from e
    msize, msha = len(data), sha_bytes(data)
    fail_identity("landmark model", msize, msha, MODEL_SIZE, MODEL_SHA)
    out_dir.mkdir(parents=True, exist_ok=True)
    model = out_dir / ENTRY
    if not model.exists() or model.read_bytes() != data: model.write_bytes(data)
    return model, {
        "status":"PASS", "bundle_path":str(bundle), "bundle_size":bsize, "bundle_sha256":bsha,
        "model_path":str(model), "model_size":msize, "model_sha256":msha,
    }

def etype(port: Any) -> str:
    try: t = port.get_element_type()
    except Exception: t = getattr(port, "element_type", "unknown")
    try: return str(t.get_type_name())
    except Exception: return str(t)

def shape_of(port: Any) -> tuple[int,...] | None:
    try: return tuple(int(x) for x in port.shape)
    except Exception: pass
    try:
        p = port.get_partial_shape()
        if p.is_static: return tuple(int(x) for x in p.to_shape())
    except Exception: pass
    return None

def names_of(port: Any) -> list[str]:
    try: return sorted(str(x) for x in port.get_names())
    except Exception:
        try: return [str(port.get_any_name())]
        except Exception: return []

def item_size(t: str) -> int | None:
    t = t.lower()
    for k,v in {"f64":8,"float64":8,"f32":4,"float32":4,"f16":2,"float16":2,"bf16":2,
                "i64":8,"int64":8,"i32":4,"int32":4,"i16":2,"int16":2,"i8":1,"int8":1,
                "u64":8,"uint64":8,"u32":4,"uint32":4,"u16":2,"uint16":2,"u8":1,"uint8":1,
                "bool":1,"boolean":1}.items():
        if t == k or f"'{k}'" in t: return v
    return None

def port_desc(i: int, p: Any) -> dict[str, Any]:
    s, t = shape_of(p), etype(p)
    n = math.prod(s) if s else None
    b = item_size(t)
    return {"index":i,"names":names_of(p),"dtype":t,"shape":list(s) if s else None,
            "element_count":n,"byte_size":n*b if n is not None and b else None}

def inspect_model(model: Any) -> dict[str, Any]:
    ins = [port_desc(i,p) for i,p in enumerate(model.inputs)]
    outs = [port_desc(i,p) for i,p in enumerate(model.outputs)]
    in_ok = len(ins)==1 and tuple(ins[0]["shape"] or ())==INPUT_SHAPE and ins[0]["dtype"].lower() in {"f32","float32"}
    out_ok = Counter(tuple(x["shape"] or ()) for x in outs)==Counter(OUTPUT_SHAPES)
    dtype_ok = all(x["dtype"].lower() in {"f32","float32"} for x in outs)
    small = [x["index"] for x in outs if tuple(x["shape"] or ()) in SMALL_SHAPES]
    vals = sum(outs[i]["element_count"] or 0 for i in small)
    bytes_ = sum(outs[i]["byte_size"] or 0 for i in small)
    return {"inputs":ins,"outputs":outs,"contract_match":in_ok and out_ok and dtype_ok,
            "input_contract_match":in_ok,"output_shape_contract_match":out_ok,"outputs_float32":dtype_ok,
            "expected_input_shape":list(INPUT_SHAPE),"expected_output_shapes":[list(x) for x in OUTPUT_SHAPES],
            "small_output_indices":small,"small_output_values":vals,"small_output_bytes":bytes_}

def stats(values: Sequence[float]) -> dict[str, Any]:
    a = sorted(float(x) for x in values)
    if not a: return {k:0.0 for k in ("mean_ms","p50_ms","p95_ms","p99_ms","min_ms","max_ms","stddev_ms","rate_per_second")} | {"count":0}
    def pct(p: float) -> float:
        q=(len(a)-1)*p; lo,hi=math.floor(q),math.ceil(q)
        return a[lo] if lo==hi else a[lo]+(a[hi]-a[lo])*(q-lo)
    mean=statistics.fmean(a)
    return {"count":len(a),"mean_ms":mean,"p50_ms":pct(.5),"p95_ms":pct(.95),"p99_ms":pct(.99),
            "min_ms":a[0],"max_ms":a[-1],"stddev_ms":statistics.pstdev(a) if len(a)>1 else 0.0,
            "rate_per_second":1000.0/mean if mean>0 else 0.0}

def safe(v: Any) -> Any:
    if v is None or isinstance(v,(str,int,float,bool)): return v
    if isinstance(v,dict): return {str(k):safe(x) for k,x in v.items()}
    if isinstance(v,(list,tuple,set)): return [safe(x) for x in v]
    if hasattr(v,"tolist"):
        try: return safe(v.tolist())
        except Exception: pass
    return str(v)

def prop(obj: Any, name: str) -> dict[str, Any]:
    try: return {"ok":True,"value":safe(obj.get_property(name))}
    except Exception as e: return {"ok":False,"error":f"{type(e).__name__}: {e}"}
def core_prop(core: Any, dev: str, name: str) -> dict[str, Any]:
    try: return {"ok":True,"value":safe(core.get_property(dev,name))}
    except Exception as e: return {"ok":False,"error":f"{type(e).__name__}: {e}"}
def device_props(core: Any, dev: str) -> dict[str, Any]:
    keys=("FULL_DEVICE_NAME","DEVICE_ARCHITECTURE","DEVICE_TYPE","OPTIMIZATION_CAPABILITIES","SUPPORTED_PROPERTIES",
          "RANGE_FOR_ASYNC_INFER_REQUESTS","RANGE_FOR_STREAMS","NUM_STREAMS","INFERENCE_NUM_THREADS","PERFORMANCE_HINT")
    return {k:core_prop(core,dev,k) for k in keys}
def resolve(req: str, available: Sequence[str]) -> str | None:
    u=req.upper(); exact=[x for x in available if str(x).upper()==u]
    if exact: return str(exact[0])
    indexed=[x for x in available if str(x).upper().startswith(u+".")]
    return str(indexed[0]) if indexed else None

def validate_devices(values: Sequence[str]) -> list[str]:
    out=[]
    for raw in values:
        v=raw.upper()
        if v not in {"CPU","GPU"}: raise ValueError(f"Device {raw!r} forbidden; use explicit CPU/GPU only (no AUTO/HETERO).")
        if v not in out: out.append(v)
    if not out: raise ValueError("At least one device required")
    return out

def input_data() -> Any:
    import numpy as np
    return np.random.default_rng(20260913).random(INPUT_SHAPE,dtype=np.float32)
def copy_proxy(req: Any, indices: Sequence[int], count: int) -> dict[str, Any]:
    import numpy as np
    times=[]; bytes_=0
    for _ in range(count):
        t=time.perf_counter_ns(); arrays=[np.array(req.get_output_tensor(i).data,copy=True) for i in indices]; e=time.perf_counter_ns()
        times.append((e-t)/1e6); bytes_=sum(x.nbytes for x in arrays)
    return {"label":"post-infer host materialization/copy proxy","indices":list(indices),"bytes_per_set":bytes_,"statistics":stats(times)}
def sentis_delta(s: dict[str,Any]) -> dict[str,float]:
    m,r=s["mean_ms"],s["rate_per_second"]
    return {"sentis_cpu_mean_ms":SENTIS_MEAN_MS,"sentis_cpu_rate":SENTIS_RATE,"latency_delta_ms":m-SENTIS_MEAN_MS,
            "latency_percent_change":(m/SENTIS_MEAN_MS-1)*100,"rate_delta_per_second":r-SENTIS_RATE,
            "rate_percent_change":(r/SENTIS_RATE-1)*100}
def run_device(ov: Any, core: Any, model: Any, req_dev: str, available: Sequence[str], data: Any, contract: dict[str,Any], warm: int, iters: int, copies: int) -> dict[str,Any]:
    out={"requested_device":req_dev,"status":"NOT_RUN","warmup_iterations":warm,"measured_iterations":iters,"copy_iterations":copies}
    dev=resolve(req_dev,available); out["resolved_device"]=dev
    if not dev:
        out.update(status="UNAVAILABLE",error=f"No explicit {req_dev} in core.available_devices; no AUTO/HETERO/CPU fallback attempted.")
        return out
    out["device_properties"]=device_props(core,dev)
    try:
        supported=core.get_property(dev,"SUPPORTED_PROPERTIES")
        hint=any("PERFORMANCE_HINT" in str(x).upper() for x in supported)
    except Exception: hint=True
    cfg={"PERFORMANCE_HINT":"LATENCY"} if hint else {}
    out["compile_config"],out["latency_hint_applied"]=cfg,hint
    try:
        t=time.perf_counter_ns(); compiled=core.compile_model(model,dev,cfg); e=time.perf_counter_ns(); out["compile_time_ms"]=(e-t)/1e6
    except Exception as ex:
        out.update(status="COMPILE_FAILED",error=f"{type(ex).__name__}: {ex}",traceback=traceback.format_exc()); return out
    out["compiled_properties"]={k:prop(compiled,k) for k in ("EXECUTION_DEVICES","PERFORMANCE_HINT","SUPPORTED_PROPERTIES")}
    try:
        req=compiled.create_infer_request(); req.set_input_tensor(0,ov.Tensor(data))
        for _ in range(warm): req.infer(share_outputs=True)
        timings=[]
        for _ in range(iters):
            t=time.perf_counter_ns(); req.infer(share_outputs=True); e=time.perf_counter_ns(); timings.append((e-t)/1e6)
        s=stats(timings); out["latency"]=s; out["delta_vs_sentis_cpu"]=sentis_delta(s)
        small=contract["small_output_indices"]; allidx=list(range(len(contract["outputs"])))
        out["small_output_copy_proxy"]=copy_proxy(req,small,copies); out["all_output_copy_proxy"]=copy_proxy(req,allidx,copies)
        import numpy as np
        out["_sanity"]=[np.array(req.get_output_tensor(i).data,copy=True) for i in allidx]; out["status"]="SUCCESS"
    except Exception as ex:
        out.update(status="RUNTIME_FAILED",error=f"{type(ex).__name__}: {ex}",traceback=traceback.format_exc())
    return out

def compare(cpu: dict[str,Any], gpu: dict[str,Any]) -> dict[str,Any] | None:
    if cpu.get("status")!="SUCCESS" or gpu.get("status")!="SUCCESS": return None
    import numpy as np
    result=[]
    for i,(a,b) in enumerate(zip(cpu["_sanity"],gpu["_sanity"])):
        a=np.asarray(a); b=np.asarray(b); same=a.shape==b.shape
        x={"index":i,"cpu_shape":list(a.shape),"gpu_shape":list(b.shape),"shape_match":same,
           "cpu_finite":bool(np.isfinite(a).all()),"gpu_finite":bool(np.isfinite(b).all()),
           "cpu_nan_count":int(np.isnan(a).sum()),"gpu_nan_count":int(np.isnan(b).sum()),
           "cpu_inf_count":int(np.isinf(a).sum()),"gpu_inf_count":int(np.isinf(b).sum())}
        if same:
            f=np.isfinite(a)&np.isfinite(b)
            if f.any():
                d=np.abs(a.astype(np.float64,copy=False)[f]-b.astype(np.float64,copy=False)[f])
                x.update(max_absolute_difference=float(d.max()),mean_absolute_difference=float(d.mean()),rms_difference=float(np.sqrt(np.mean(d*d))))
        result.append(x)
    return {"scope":"OpenVINO CPU-vs-GPU raw-network consistency only; not LiteRT/MediaPipe semantic equivalence.","outputs":result}
def clean_result(r: dict[str,Any]) -> dict[str,Any]:
    c=dict(r); back={}
    for k,v in r.get("backends",{}).items(): q=dict(v); q.pop("_sanity",None); back[k]=q
    c["backends"]=back; return safe(c)
def fmt(s: dict[str,Any]) -> str:
    return (f"mean={s['mean_ms']:.3f}ms p50={s['p50_ms']:.3f} p95={s['p95_ms']:.3f} p99={s['p99_ms']:.3f} "
            f"min={s['min_ms']:.3f} max={s['max_ms']:.3f} std={s['stddev_ms']:.3f} rate={s['rate_per_second']:.3f}/s")
def text_report(r: dict[str,Any]) -> str:
    L=["Golden Needle — OpenVINO Exact-Landmark Benchmark",f"status: {r.get('status')}",f"timestamp_utc: {r.get('timestamp_utc')}",f"harness_version: {r.get('harness_version')}",""]
    for section in ("environment","config","model_identity","model_read"):
        if section in r:
            L.append(section+":"); L += [f"  {k}: {v}" for k,v in r[section].items()]; L.append("")
    if r.get("model_contract"):
        c=r["model_contract"]; L.append(f"model_contract: match={c['contract_match']}")
        for x in c["inputs"]: L.append(f"  input[{x['index']}] names={x['names']} dtype={x['dtype']} shape={x['shape']} elements={x['element_count']} bytes={x['byte_size']}")
        for x in c["outputs"]: L.append(f"  output[{x['index']}] names={x['names']} dtype={x['dtype']} shape={x['shape']} elements={x['element_count']} bytes={x['byte_size']}")
        L.append(f"  small outputs indices={c['small_output_indices']} values={c['small_output_values']} bytes={c['small_output_bytes']}"); L.append("")
    L.append(f"available_devices: {r.get('available_devices',[])}")
    for dev,props in r.get("device_inventory",{}).items(): L.append(f"  {dev}: {props}")
    L.append("")
    for name,b in r.get("backends",{}).items():
        L += [f"{name}: status={b.get('status')} resolved={b.get('resolved_device')} compile_ms={b.get('compile_time_ms')}",f"  compile_config={b.get('compile_config')}",f"  compiled_properties={b.get('compiled_properties')}"]
        if b.get("latency"): L.append("  latency: "+fmt(b["latency"]))
        for key in ("small_output_copy_proxy","all_output_copy_proxy"):
            if b.get(key): L.append(f"  {key}: bytes={b[key]['bytes_per_set']} {fmt(b[key]['statistics'])}")
        if b.get("delta_vs_sentis_cpu"): L.append(f"  vs Sentis CPU 46.24ms/21.63s: {b['delta_vs_sentis_cpu']}")
        if b.get("error"): L.append(f"  error: {b['error']}")
        if b.get("traceback"): L.append("  traceback:\n"+b["traceback"])
        L.append("")
    if r.get("numerical_sanity"):
        L.append("numerical_sanity:"); L.append("  "+r["numerical_sanity"]["scope"])
        for x in r["numerical_sanity"]["outputs"]: L.append("  "+str(x))
    if r.get("fatal_error"): L += ["","fatal_error: "+r["fatal_error"],r.get("fatal_traceback","")]
    return "\n".join(L).rstrip()+"\n"
def write_reports(r: dict[str,Any], out: Path, stem: str) -> tuple[Path,Path]:
    out.mkdir(parents=True,exist_ok=True); c=clean_result(r); jp=out/(stem+".json"); tp=out/(stem+".txt")
    jp.write_text(json.dumps(c,indent=2,sort_keys=True),encoding="utf-8"); tp.write_text(text_report(c),encoding="utf-8"); return tp,jp
def summary(r: dict[str,Any], tp: Path, jp: Path) -> str:
    L=["=== GOLDEN NEEDLE OPENVINO SUMMARY ===",f"overall={r.get('status')}",f"openvino={r.get('environment',{}).get('openvino_version','n/a')}",f"exact_model_identity={r.get('model_identity',{}).get('status','n/a')}",f"available_devices={r.get('available_devices',[])}"]
    for n,b in r.get("backends",{}).items():
        x=f"{n}: status={b.get('status')} resolved={b.get('resolved_device')}"
        if b.get("latency"): s=b["latency"]; x+=f" mean={s['mean_ms']:.3f}ms p50={s['p50_ms']:.3f} p95={s['p95_ms']:.3f} p99={s['p99_ms']:.3f} rate={s['rate_per_second']:.3f}/s"
        if b.get("error"): x+=f" error={b['error']}"
        L.append(x)
    L += [f"text_report={tp}",f"json_report={jp}","=== END GOLDEN NEEDLE OPENVINO SUMMARY ==="]
    return "\n".join(L)
def ensure_python() -> None:
    if struct.calcsize("P")*8!=64 or not ((3,10)<=sys.version_info[:2]<=(3,14)): raise BenchError("Requires 64-bit Python 3.10-3.14")
def benchmark(args: argparse.Namespace) -> tuple[dict[str,Any],Path,Path]:
    tool=Path(__file__).resolve().parent; root=args.repo_root.resolve(); stamp=datetime.now().strftime("%Y%m%d-%H%M%S")
    r={"harness_version":VERSION,"timestamp_utc":datetime.now(timezone.utc).isoformat(timespec="seconds"),"status":"STARTED",
       "config":{"openvino_pin":OV_PIN,"warmup":args.warmup,"iterations":args.iterations,"copy_iterations":args.copy_iterations,"devices":args.devices,"performance_hint":"LATENCY","serial_requests":1,"batching":False,"auto_hetero":False},
       "environment":{"os":platform.platform(),"python":sys.version.replace("\n"," "),"architecture":platform.machine(),"pointer_bits":struct.calcsize("P")*8},"backends":{}}
    try:
        ensure_python(); model_path,ident=extract_exact(root/BUNDLE_REL,tool/"artifacts"); r["model_identity"]=ident
        try: import openvino as ov
        except Exception as e: raise BenchError(f"OpenVINO import failed: {type(e).__name__}: {e}; use run.ps1") from e
        r["environment"]["openvino_version"]=getattr(ov,"__version__","unknown"); r["environment"]["openvino_package_version"]=importlib.metadata.version("openvino")
        if r["environment"]["openvino_package_version"]!=OV_PIN: raise BenchError(f"Expected OpenVINO package {OV_PIN}, got {r['environment']['openvino_package_version']}")
        core=ov.Core(); avail=[str(x) for x in core.available_devices]; r["available_devices"]=avail; r["device_inventory"]={d:device_props(core,d) for d in avail}
        t=time.perf_counter_ns()
        try: model=core.read_model(str(model_path))
        except Exception as e:
            r["model_read"]={"status":"FAILED","time_ms":(time.perf_counter_ns()-t)/1e6,"error":f"{type(e).__name__}: {e}","traceback":traceback.format_exc()}; raise BenchError("Core.read_model failed for exact TFLite") from e
        r["model_read"]={"status":"SUCCESS","time_ms":(time.perf_counter_ns()-t)/1e6}; c=inspect_model(model); r["model_contract"]=c
        if not c["contract_match"]: raise BenchError("OpenVINO contract mismatch; failing closed before timing")
        if c["small_output_values"]!=313 or c["small_output_bytes"]!=1252: raise BenchError("Small output proxy is not 313 float32 / 1,252 bytes")
        data=input_data()
        for d in args.devices: r["backends"][d]=run_device(ov,core,model,d,avail,data,c,args.warmup,args.iterations,args.copy_iterations)
        r["numerical_sanity"]=compare(r["backends"].get("CPU",{}),r["backends"].get("GPU",{}))
        r["status"]="COMPLETE" if any(x.get("status")=="SUCCESS" for x in r["backends"].values()) else "NO_BACKEND_SUCCEEDED"
    except Exception as e:
        r["status"]="FAILED_CLOSED"; r["fatal_error"]=f"{type(e).__name__}: {e}"; r["fatal_traceback"]=traceback.format_exc(); r.setdefault("model_identity",{"status":"FAILED_OR_NOT_VERIFIED"})
    tp,jp=write_reports(r,tool/"results",f"openvino-landmark-{stamp}"); cr=clean_result(r); print(text_report(cr)); print(summary(cr,tp,jp)); return r,tp,jp
def self_test() -> int:
    failures=[]
    if stats([1,2,3,4])["mean_ms"]!=2.5: failures.append("stats")
    try: validate_devices(["AUTO"]); failures.append("AUTO rejection")
    except ValueError: pass
    if resolve("GPU",["CPU"]) is not None or resolve("GPU",["CPU","GPU.0"])!="GPU.0": failures.append("device resolution")
    class Fake:
        def get_property(self,d,n): return ["PERFORMANCE_HINT"] if n=="SUPPORTED_PROPERTIES" else "fake"
        def compile_model(self,*a,**k): raise RuntimeError("synthetic compile failure")
    x=run_device(None,Fake(),object(),"GPU",["GPU.0"],None,{"small_output_indices":[],"outputs":[]},1,1,1)
    if x.get("status")!="COMPILE_FAILED": failures.append("compile failure")
    y=run_device(None,Fake(),object(),"GPU",["CPU"],None,{"small_output_indices":[],"outputs":[]},1,1,1)
    if y.get("status")!="UNAVAILABLE": failures.append("missing GPU")
    with tempfile.TemporaryDirectory() as td:
        p=Path(td); data=b"synthetic exact model"; bundle=p/"b.bytes"
        with zipfile.ZipFile(bundle,"w",compression=zipfile.ZIP_STORED) as z: z.writestr("x/"+ENTRY,data)
        bs,bh=bundle.stat().st_size,sha_file(bundle); ms,mh=len(data),sha_bytes(data)
        global BUNDLE_SIZE,BUNDLE_SHA,MODEL_SIZE,MODEL_SHA
        old=(BUNDLE_SIZE,BUNDLE_SHA,MODEL_SIZE,MODEL_SHA); BUNDLE_SIZE,BUNDLE_SHA,MODEL_SIZE,MODEL_SHA=bs,bh,ms,mh
        try:
            q,_=extract_exact(bundle,p/"a")
            if q.read_bytes()!=data: failures.append("extract")
        except Exception: failures.append("extract")
        finally: BUNDLE_SIZE,BUNDLE_SHA,MODEL_SIZE,MODEL_SHA=old
        write_reports({"status":"SELF_TEST","backends":{}},p/"r","test")
    if failures: print("SELF-TEST FAILED: "+", ".join(failures),file=sys.stderr); return 1
    print("SELF-TEST PASS"); return 0
def main() -> int:
    ap=argparse.ArgumentParser(description="Golden Needle isolated OpenVINO exact-landmark benchmark")
    ap.add_argument("--repo-root",type=Path,default=Path(__file__).resolve().parents[2]); ap.add_argument("--warmup",type=int,default=30); ap.add_argument("--iterations",type=int,default=300); ap.add_argument("--copy-iterations",type=int,default=100); ap.add_argument("--devices",nargs="+",default=["CPU","GPU"]); ap.add_argument("--self-test",action="store_true")
    a=ap.parse_args()
    if a.self_test: return self_test()
    try: a.devices=validate_devices(a.devices)
    except ValueError as e: ap.error(str(e))
    if a.warmup<0: ap.error("--warmup must be >= 0")
    if a.iterations<=0 or a.copy_iterations<=0: ap.error("--iterations and --copy-iterations must be > 0")
    r,_,_=benchmark(a); return 0 if r.get("status")=="COMPLETE" else 2
if __name__=="__main__": raise SystemExit(main())
