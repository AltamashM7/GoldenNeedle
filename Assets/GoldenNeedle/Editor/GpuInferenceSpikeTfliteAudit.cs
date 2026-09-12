#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GoldenNeedle.EditorTools
{
    public static partial class GpuInferenceSpikeSetup
    {
        sealed class TfliteAudit
        {
            static readonly HashSet<string> OrdinaryBuiltinConsumerNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "ADD", "CONCATENATION", "CONV_2D", "DEPTHWISE_CONV_2D", "DEQUANTIZE",
                "DEPTH_TO_SPACE", "FULLY_CONNECTED", "LOGISTIC", "MAX_POOL_2D", "MUL",
                "PAD", "RESHAPE", "RESIZE_BILINEAR", "TRANSPOSE",
            };

            public readonly List<TensorInfo> Inputs = new List<TensorInfo>();
            public readonly List<TensorInfo> Outputs = new List<TensorInfo>();
            public readonly SortedDictionary<string, int> OperatorCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public readonly List<DensifyOperatorAudit> DensifyOperators = new List<DensifyOperatorAudit>();
            public int TensorCount;
            public int QuantizedTensorCount;
            public int OperatorCount;

            public static TfliteAudit Read(byte[] data)
            {
                var reader = new FlatBufferReader(data);
                var model = reader.RootTable();
                var operatorCodesVector = reader.Vector(model, 1);
                var subgraphsVector = reader.Vector(model, 2);
                var buffersVector = reader.Vector(model, 4);
                if (subgraphsVector.Count == 0)
                    throw new InvalidDataException("TFLite model contains no subgraphs.");
                if (subgraphsVector.Count != 1)
                    throw new InvalidDataException("GPU spike audit currently expects one TFLite subgraph; found " + subgraphsVector.Count + ".");

                var audit = new TfliteAudit();
                var subgraph = reader.VectorTable(subgraphsVector, 0);
                var tensorsVector = reader.Vector(subgraph, 0);
                var inputsVector = reader.Vector(subgraph, 1);
                var outputsVector = reader.Vector(subgraph, 2);
                var operatorsVector = reader.Vector(subgraph, 3);
                var tensors = new TensorInfo[tensorsVector.Count];
                audit.TensorCount = tensors.Length;

                for (var tensorIndex = 0; tensorIndex < tensors.Length; tensorIndex++)
                {
                    var tensorTable = reader.VectorTable(tensorsVector, tensorIndex);
                    tensors[tensorIndex] = ReadTensorInfo(reader, buffersVector, tensorTable, tensorIndex);
                    if (reader.TableOffset(tensorTable, 4) != 0)
                    {
                        var quant = reader.Table(tensorTable, 4);
                        if (reader.Vector(quant, 2).Count > 0)
                            audit.QuantizedTensorCount++;
                    }
                }

                for (var i = 0; i < inputsVector.Count; i++)
                    audit.Inputs.Add(RequireTensor(tensors, reader.VectorInt(inputsVector, i)));
                for (var i = 0; i < outputsVector.Count; i++)
                    audit.Outputs.Add(RequireTensor(tensors, reader.VectorInt(outputsVector, i)));

                var operators = new OperatorInfo[operatorsVector.Count];
                audit.OperatorCount = operators.Length;
                var producerByTensor = new int[tensors.Length];
                for (var i = 0; i < producerByTensor.Length; i++) producerByTensor[i] = -1;
                var consumersByTensor = new List<int>[tensors.Length];
                for (var i = 0; i < consumersByTensor.Length; i++) consumersByTensor[i] = new List<int>();

                for (var opIndex = 0; opIndex < operatorsVector.Count; opIndex++)
                {
                    var op = reader.VectorTable(operatorsVector, opIndex);
                    var opcodeIndex = checked((int)reader.UInt(op, 0, 0));
                    if (opcodeIndex < 0 || opcodeIndex >= operatorCodesVector.Count)
                        throw new InvalidDataException("TFLite operator references invalid opcode index.");

                    var opcode = reader.VectorTable(operatorCodesVector, opcodeIndex);
                    var deprecated = reader.Byte(opcode, 0, 0);
                    var builtin = reader.Int(opcode, 3, deprecated);
                    var custom = reader.String(opcode, 1);
                    var version = reader.Int(opcode, 2, 1);
                    var name = builtin == 32 && !string.IsNullOrEmpty(custom) ? "CUSTOM:" + custom : BuiltinOperatorName(builtin);
                    var inputIndices = ReadIntVector(reader, reader.Vector(op, 1));
                    var outputIndices = ReadIntVector(reader, reader.Vector(op, 2));
                    operators[opIndex] = new OperatorInfo(opIndex, builtin, name, version, inputIndices, outputIndices);

                    var countKey = name + "@v" + version;
                    audit.OperatorCounts.TryGetValue(countKey, out var count);
                    audit.OperatorCounts[countKey] = count + 1;

                    for (var i = 0; i < inputIndices.Length; i++)
                    {
                        var tensorIndex = inputIndices[i];
                        if (tensorIndex >= 0 && tensorIndex < consumersByTensor.Length)
                            consumersByTensor[tensorIndex].Add(opIndex);
                    }
                    for (var i = 0; i < outputIndices.Length; i++)
                    {
                        var tensorIndex = outputIndices[i];
                        if (tensorIndex < 0 || tensorIndex >= producerByTensor.Length) continue;
                        if (producerByTensor[tensorIndex] >= 0)
                            throw new InvalidDataException("Tensor has more than one producer op: " + tensorIndex + ".");
                        producerByTensor[tensorIndex] = opIndex;
                    }
                }

                for (var opIndex = 0; opIndex < operators.Length; opIndex++)
                {
                    var op = operators[opIndex];
                    if (op.BuiltinCode == 124)
                        audit.DensifyOperators.Add(BuildDensifyAudit(reader, tensorsVector, tensors, operators, producerByTensor, consumersByTensor, op));
                }
                return audit;
            }

            static DensifyOperatorAudit BuildDensifyAudit(
                FlatBufferReader reader, FlatBufferReader.VectorRef tensorsVector, TensorInfo[] tensors,
                OperatorInfo[] operators, int[] producerByTensor, List<int>[] consumersByTensor, OperatorInfo op)
            {
                var inputs = new List<DensifyTensorInput>();
                long sparseValueBytes = 0;
                long sparseMetadataBytes = 0;
                var isStatic = op.Inputs.Length > 0;
                for (var i = 0; i < op.Inputs.Length; i++)
                {
                    var tensorIndex = op.Inputs[i];
                    if (tensorIndex < 0) continue;
                    var tensor = RequireTensor(tensors, tensorIndex);
                    var tensorTable = reader.VectorTable(tensorsVector, tensorIndex);
                    var producer = producerByTensor[tensorIndex];
                    var isConstant = tensor.BufferBytes > 0 && producer < 0;
                    var sparsity = ReadSparsity(reader, tensorTable);
                    inputs.Add(new DensifyTensorInput(tensor, producer, isConstant, sparsity));
                    isStatic &= isConstant;
                    if (isConstant) sparseValueBytes += tensor.BufferBytes;
                    sparseMetadataBytes += sparsity.MetadataValueBytes;
                }

                var outputs = new List<DensifyTensorOutput>();
                long denseBytes = 0;
                var ordinaryConsumers = true;
                var hasConsumers = false;
                for (var i = 0; i < op.Outputs.Length; i++)
                {
                    var tensorIndex = op.Outputs[i];
                    if (tensorIndex < 0) continue;
                    var tensor = RequireTensor(tensors, tensorIndex);
                    var consumerNames = new List<string>();
                    foreach (var consumerIndex in consumersByTensor[tensorIndex])
                    {
                        hasConsumers = true;
                        var consumer = operators[consumerIndex];
                        consumerNames.Add("op[" + consumer.Index + "] " + consumer.Name + "@v" + consumer.Version);
                        if (!OrdinaryBuiltinConsumerNames.Contains(consumer.Name)) ordinaryConsumers = false;
                    }
                    var estimate = EstimateDenseBytes(tensor);
                    if (estimate >= 0) denseBytes += estimate;
                    outputs.Add(new DensifyTensorOutput(tensor, consumerNames.ToArray(), estimate));
                }
                if (!hasConsumers) ordinaryConsumers = false;
                return new DensifyOperatorAudit(op.Index, op.Version, inputs.ToArray(), outputs.ToArray(), isStatic, ordinaryConsumers,
                    sparseValueBytes, sparseMetadataBytes, denseBytes);
            }

            static SparsityInfo ReadSparsity(FlatBufferReader reader, int tensorTable)
            {
                if (reader.TableOffset(tensorTable, 6) == 0) return SparsityInfo.Absent;
                var sparsity = reader.Table(tensorTable, 6);
                var traversalOrder = ReadIntVector(reader, reader.Vector(sparsity, 0));
                var blockMap = ReadIntVector(reader, reader.Vector(sparsity, 1));
                var dimensionsVector = reader.Vector(sparsity, 2);
                var dimensions = new DimensionSparsityInfo[dimensionsVector.Count];
                long metadataBytes = checked((long)(traversalOrder.Length + blockMap.Length) * sizeof(int));
                for (var i = 0; i < dimensions.Length; i++)
                {
                    var dimension = reader.VectorTable(dimensionsVector, i);
                    var formatCode = reader.Byte(dimension, 0, 0);
                    var denseSize = reader.Int(dimension, 1, 0);
                    var segments = ReadSparseIndexVector(reader, dimension, 2, 3);
                    var indices = ReadSparseIndexVector(reader, dimension, 4, 5);
                    metadataBytes += segments.ValueBytes + indices.ValueBytes;
                    dimensions[i] = new DimensionSparsityInfo(
                        formatCode == 0 ? "DENSE" : formatCode == 1 ? "SPARSE_CSR" : "DIMENSION_TYPE_" + formatCode,
                        denseSize, segments, indices);
                }
                return new SparsityInfo(true, traversalOrder, blockMap, dimensions, metadataBytes);
            }

            static SparseIndexVectorInfo ReadSparseIndexVector(FlatBufferReader reader, int table, int typeField, int valueField)
            {
                var type = reader.Byte(table, typeField, 0);
                if (type == 0 || reader.TableOffset(table, valueField) == 0) return SparseIndexVectorInfo.None;
                var values = reader.Vector(reader.Table(table, valueField), 0);
                var ints = new int[values.Count];
                if (type == 1)
                {
                    for (var i = 0; i < ints.Length; i++) ints[i] = reader.VectorInt(values, i);
                    return new SparseIndexVectorInfo("Int32Vector", ints.Length, checked((long)ints.Length * sizeof(int)), SummarizeValues(ints));
                }
                if (type == 2)
                {
                    for (var i = 0; i < ints.Length; i++) ints[i] = reader.VectorUInt16(values, i);
                    return new SparseIndexVectorInfo("Uint16Vector", ints.Length, checked((long)ints.Length * sizeof(ushort)), SummarizeValues(ints));
                }
                if (type == 3)
                {
                    for (var i = 0; i < ints.Length; i++) ints[i] = reader.VectorByte(values, i);
                    return new SparseIndexVectorInfo("Uint8Vector", ints.Length, ints.LongLength, SummarizeValues(ints));
                }
                return new SparseIndexVectorInfo("SparseIndexVectorType_" + type, values.Count, -1, "unrecognized union type");
            }

            static string SummarizeValues(int[] values)
            {
                if (values == null || values.Length == 0) return "[]";
                if (values.Length <= 20) return "[" + string.Join(",", values) + "]";
                var builder = new StringBuilder("[");
                for (var i = 0; i < 16; i++) { if (i > 0) builder.Append(','); builder.Append(values[i]); }
                builder.Append(",…,");
                for (var i = values.Length - 4; i < values.Length; i++) { if (i > values.Length - 4) builder.Append(','); builder.Append(values[i]); }
                return builder.Append(']').ToString();
            }

            static int[] ReadIntVector(FlatBufferReader reader, FlatBufferReader.VectorRef vector)
            {
                var values = new int[vector.Count];
                for (var i = 0; i < values.Length; i++) values[i] = reader.VectorInt(vector, i);
                return values;
            }

            static TensorInfo ReadTensorInfo(FlatBufferReader reader, FlatBufferReader.VectorRef buffers, int table, int index)
            {
                var shape = ReadIntVector(reader, reader.Vector(table, 0));
                var typeCode = reader.Byte(table, 1, 0);
                var bufferIndex = checked((int)reader.UInt(table, 2, 0));
                long bufferBytes = 0;
                if (bufferIndex >= 0 && bufferIndex < buffers.Count)
                    bufferBytes = reader.Vector(reader.VectorTable(buffers, bufferIndex), 0).Count;
                return new TensorInfo(index, reader.String(table, 3) ?? string.Empty, typeCode, TensorTypeName(typeCode), shape,
                    bufferIndex, bufferBytes, reader.TableOffset(table, 6) != 0);
            }

            static TensorInfo RequireTensor(TensorInfo[] tensors, int index)
            {
                if (index < 0 || index >= tensors.Length) throw new InvalidDataException("TFLite tensor index is invalid: " + index + ".");
                return tensors[index];
            }

            static long EstimateDenseBytes(TensorInfo tensor)
            {
                var bytes = TensorTypeBytes(tensor.TypeCode);
                if (bytes <= 0) return -1;
                long elements = 1;
                foreach (var dimension in tensor.Shape)
                {
                    if (dimension < 0) return -1;
                    elements = checked(elements * dimension);
                }
                return checked(elements * bytes);
            }

            public string ToReport(string label)
            {
                var builder = new StringBuilder();
                builder.AppendLine(label + ":");
                builder.AppendLine($"  tensors={TensorCount} operators={OperatorCount} quantizedTensors={QuantizedTensorCount} quantization={(QuantizedTensorCount == 0 ? "none detected" : "present")}");
                for (var i = 0; i < Inputs.Count; i++) builder.AppendLine("  input[" + i + "] " + Inputs[i]);
                for (var i = 0; i < Outputs.Count; i++) builder.AppendLine("  output[" + i + "] " + Outputs[i]);
                builder.AppendLine("  TFLite operator counts:");
                foreach (var pair in OperatorCounts) builder.AppendLine("    " + pair.Key + " x" + pair.Value);
                builder.AppendLine("  DENSIFY audit:");
                builder.AppendLine("    count=" + DensifyOperators.Count);
                if (DensifyOperators.Count == 0) { builder.AppendLine("    no DENSIFY operators present"); return builder.ToString(); }

                var uniqueInputs = new HashSet<int>();
                var uniqueOutputs = new HashSet<int>();
                long sparseValues = 0, sparseMetadata = 0, denseValues = 0;
                var allStatic = true;
                var allSparseMetadata = true;
                var ordinaryConsumers = true;
                var denseKnown = true;
                foreach (var densify in DensifyOperators)
                {
                    builder.Append(densify.ToReport("    "));
                    allStatic &= densify.StaticWrtModelInput;
                    ordinaryConsumers &= densify.OnlyOrdinaryBuiltinConsumers;
                    foreach (var input in densify.Inputs)
                    {
                        allSparseMetadata &= input.Sparsity.Present;
                        if (input.IsConstant && uniqueInputs.Add(input.Tensor.Index))
                        {
                            sparseValues += input.Tensor.BufferBytes;
                            sparseMetadata += input.Sparsity.MetadataValueBytes;
                        }
                    }
                    foreach (var output in densify.Outputs)
                    {
                        if (output.EstimatedDenseBytes < 0) { denseKnown = false; continue; }
                        if (uniqueOutputs.Add(output.Tensor.Index)) denseValues += output.EstimatedDenseBytes;
                    }
                }
                var safeCandidate = allStatic && allSparseMetadata && ordinaryConsumers && denseKnown;
                builder.AppendLine("    aggregate:");
                builder.AppendLine("      allDensifyInputsStaticConstants=" + allStatic);
                builder.AppendLine("      allDensifyInputsHaveSparsityMetadata=" + allSparseMetadata);
                builder.AppendLine("      allDensifyOutputsOnlyOrdinaryBuiltinConsumers=" + ordinaryConsumers + " (dataflow classification only; not a Sentis support verdict)");
                builder.AppendLine("      denseStorageEstimatesKnown=" + denseKnown);
                builder.AppendLine("      uniqueSparseInputTensors=" + uniqueInputs.Count);
                builder.AppendLine("      uniqueDenseOutputTensors=" + uniqueOutputs.Count);
                builder.AppendLine("      sparseStoredValueBytes=" + sparseValues);
                builder.AppendLine("      sparseIndexMetadataValueBytes~=" + sparseMetadata + " (vector payload only; excludes FlatBuffer table/vtable overhead)");
                builder.AppendLine("      sparseStoredPlusIndexPayloadBytes~=" + (sparseValues + sparseMetadata));
                builder.AppendLine("      estimatedDenseValueBytes=" + (denseKnown ? denseValues.ToString(CultureInfo.InvariantCulture) : "UNKNOWN"));
                builder.AppendLine("      offlineDensifyAuditConclusion=" + (safeCandidate
                    ? "STATIC_STORAGE_REWRITE_CANDIDATE_IN_PRINCIPLE — sparse constants only; requires later equivalence gate; no rewrite performed"
                    : "DO_NOT_ASSUME_SAFE — dynamic input, missing sparsity metadata, non-ordinary consumer, or unknown dense storage estimate detected"));
                return builder.ToString();
            }

            static int TensorTypeBytes(int value)
            {
                switch (value)
                {
                    case 0: return 4; case 1: return 2; case 2: return 4; case 3: return 1; case 4: return 8;
                    case 6: return 1; case 7: return 2; case 8: return 8; case 9: return 1; case 10: return 8;
                    case 11: return 16; case 12: return 8; case 15: return 4; case 16: return 2; case 17: return 1; case 18: return 2;
                    default: return -1;
                }
            }

            static string TensorTypeName(int value)
            {
                switch (value)
                {
                    case 0: return "FLOAT32"; case 1: return "FLOAT16"; case 2: return "INT32"; case 3: return "UINT8";
                    case 4: return "INT64"; case 5: return "STRING"; case 6: return "BOOL"; case 7: return "INT16";
                    case 8: return "COMPLEX64"; case 9: return "INT8"; case 10: return "FLOAT64"; case 11: return "COMPLEX128";
                    case 12: return "UINT64"; case 13: return "RESOURCE"; case 14: return "VARIANT"; case 15: return "UINT32";
                    case 16: return "UINT16"; case 17: return "INT4"; case 18: return "BFLOAT16"; default: return "TENSOR_TYPE_" + value;
                }
            }

            static string BuiltinOperatorName(int value)
            {
                switch (value)
                {
                    case 0: return "ADD"; case 1: return "AVERAGE_POOL_2D"; case 2: return "CONCATENATION"; case 3: return "CONV_2D";
                    case 4: return "DEPTHWISE_CONV_2D"; case 5: return "DEPTH_TO_SPACE"; case 6: return "DEQUANTIZE"; case 9: return "FULLY_CONNECTED";
                    case 14: return "LOGISTIC"; case 17: return "MAX_POOL_2D"; case 18: return "MUL"; case 19: return "RELU";
                    case 21: return "RELU6"; case 22: return "RESHAPE"; case 23: return "RESIZE_BILINEAR"; case 25: return "SOFTMAX";
                    case 28: return "TANH"; case 32: return "CUSTOM"; case 34: return "PAD"; case 36: return "GATHER";
                    case 39: return "TRANSPOSE"; case 40: return "MEAN"; case 41: return "SUB"; case 42: return "DIV";
                    case 43: return "SQUEEZE"; case 45: return "STRIDED_SLICE"; case 47: return "EXP"; case 49: return "SPLIT";
                    case 53: return "CAST"; case 54: return "PRELU"; case 55: return "MAXIMUM"; case 57: return "MINIMUM";
                    case 59: return "NEG"; case 60: return "PADV2"; case 65: return "SLICE"; case 67: return "TRANSPOSE_CONV";
                    case 69: return "TILE"; case 70: return "EXPAND_DIMS"; case 74: return "SUM"; case 75: return "SQRT";
                    case 76: return "RSQRT"; case 77: return "SHAPE"; case 78: return "POW"; case 83: return "PACK";
                    case 88: return "UNPACK"; case 92: return "SQUARE"; case 94: return "FILL"; case 97: return "RESIZE_NEAREST_NEIGHBOR";
                    case 98: return "LEAKY_RELU"; case 101: return "ABS"; case 106: return "ADD_N"; case 114: return "QUANTIZE";
                    case 117: return "HARD_SWISH"; case 124: return "DENSIFY"; case 126: return "BATCH_MATMUL"; default: return "BUILTIN_" + value;
                }
            }
        }
    }
}
#endif
