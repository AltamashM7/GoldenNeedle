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
        readonly struct TensorInfo
        {
            public readonly int Index;
            public readonly string Name;
            public readonly int TypeCode;
            public readonly string Type;
            public readonly int[] Shape;
            public readonly int BufferIndex;
            public readonly long BufferBytes;
            public readonly bool HasSparsityMetadata;

            public TensorInfo(
                int index,
                string name,
                int typeCode,
                string type,
                int[] shape,
                int bufferIndex,
                long bufferBytes,
                bool hasSparsityMetadata)
            {
                Index = index;
                Name = name;
                TypeCode = typeCode;
                Type = type;
                Shape = shape;
                BufferIndex = bufferIndex;
                BufferBytes = bufferBytes;
                HasSparsityMetadata = hasSparsityMetadata;
            }

            public override string ToString()
            {
                return $"tensor={Index} name=\"{Name}\" type={Type} shape=[{string.Join(",", Shape)}] " +
                       $"buffer={BufferIndex} bufferBytes={BufferBytes} sparsityMetadata={HasSparsityMetadata}";
            }
        }

        readonly struct OperatorInfo
        {
            public readonly int Index;
            public readonly int BuiltinCode;
            public readonly string Name;
            public readonly int Version;
            public readonly int[] Inputs;
            public readonly int[] Outputs;

            public OperatorInfo(int index, int builtinCode, string name, int version, int[] inputs, int[] outputs)
            {
                Index = index;
                BuiltinCode = builtinCode;
                Name = name;
                Version = version;
                Inputs = inputs;
                Outputs = outputs;
            }
        }

        readonly struct SparseIndexVectorInfo
        {
            public static readonly SparseIndexVectorInfo None =
                new SparseIndexVectorInfo("NONE", 0, 0, "[]");

            public readonly string Type;
            public readonly int Count;
            public readonly long ValueBytes;
            public readonly string ValuesSummary;

            public SparseIndexVectorInfo(string type, int count, long valueBytes, string valuesSummary)
            {
                Type = type;
                Count = count;
                ValueBytes = valueBytes;
                ValuesSummary = valuesSummary;
            }

            public override string ToString()
            {
                return $"{Type} count={Count} bytes={ValueBytes} values={ValuesSummary}";
            }
        }

        readonly struct DimensionSparsityInfo
        {
            public readonly string Format;
            public readonly int DenseSize;
            public readonly SparseIndexVectorInfo Segments;
            public readonly SparseIndexVectorInfo Indices;

            public DimensionSparsityInfo(
                string format,
                int denseSize,
                SparseIndexVectorInfo segments,
                SparseIndexVectorInfo indices)
            {
                Format = format;
                DenseSize = denseSize;
                Segments = segments;
                Indices = indices;
            }
        }

        readonly struct SparsityInfo
        {
            public static readonly SparsityInfo Absent =
                new SparsityInfo(false, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<DimensionSparsityInfo>(), 0);

            public readonly bool Present;
            public readonly int[] TraversalOrder;
            public readonly int[] BlockMap;
            public readonly DimensionSparsityInfo[] Dimensions;
            public readonly long MetadataValueBytes;

            public SparsityInfo(
                bool present,
                int[] traversalOrder,
                int[] blockMap,
                DimensionSparsityInfo[] dimensions,
                long metadataValueBytes)
            {
                Present = present;
                TraversalOrder = traversalOrder;
                BlockMap = blockMap;
                Dimensions = dimensions;
                MetadataValueBytes = metadataValueBytes;
            }
        }

        readonly struct DensifyTensorInput
        {
            public readonly TensorInfo Tensor;
            public readonly int ProducerOperatorIndex;
            public readonly bool IsConstant;
            public readonly SparsityInfo Sparsity;

            public DensifyTensorInput(
                TensorInfo tensor,
                int producerOperatorIndex,
                bool isConstant,
                SparsityInfo sparsity)
            {
                Tensor = tensor;
                ProducerOperatorIndex = producerOperatorIndex;
                IsConstant = isConstant;
                Sparsity = sparsity;
            }
        }

        readonly struct DensifyTensorOutput
        {
            public readonly TensorInfo Tensor;
            public readonly string[] Consumers;
            public readonly long EstimatedDenseBytes;

            public DensifyTensorOutput(TensorInfo tensor, string[] consumers, long estimatedDenseBytes)
            {
                Tensor = tensor;
                Consumers = consumers;
                EstimatedDenseBytes = estimatedDenseBytes;
            }
        }

        readonly struct DensifyOperatorAudit
        {
            public readonly int OperatorIndex;
            public readonly int Version;
            public readonly DensifyTensorInput[] Inputs;
            public readonly DensifyTensorOutput[] Outputs;
            public readonly bool StaticWrtModelInput;
            public readonly bool OnlyOrdinaryBuiltinConsumers;
            public readonly long SparseValueBytes;
            public readonly long SparseMetadataValueBytes;
            public readonly long EstimatedDenseBytes;

            public DensifyOperatorAudit(
                int operatorIndex,
                int version,
                DensifyTensorInput[] inputs,
                DensifyTensorOutput[] outputs,
                bool staticWrtModelInput,
                bool onlyOrdinaryBuiltinConsumers,
                long sparseValueBytes,
                long sparseMetadataValueBytes,
                long estimatedDenseBytes)
            {
                OperatorIndex = operatorIndex;
                Version = version;
                Inputs = inputs;
                Outputs = outputs;
                StaticWrtModelInput = staticWrtModelInput;
                OnlyOrdinaryBuiltinConsumers = onlyOrdinaryBuiltinConsumers;
                SparseValueBytes = sparseValueBytes;
                SparseMetadataValueBytes = sparseMetadataValueBytes;
                EstimatedDenseBytes = estimatedDenseBytes;
            }

            public string ToReport(string indent)
            {
                var builder = new StringBuilder();
                builder.AppendLine(indent + "op[" + OperatorIndex + "] DENSIFY@v" + Version + ":");

                for (var i = 0; i < Inputs.Length; i++)
                {
                    var input = Inputs[i];
                    builder.AppendLine(indent + "  input[" + i + "]: " + input.Tensor);
                    builder.AppendLine(indent + "    producerOp=" +
                                       (input.ProducerOperatorIndex < 0 ? "none" : input.ProducerOperatorIndex.ToString(CultureInfo.InvariantCulture)) +
                                       " constant=" + input.IsConstant);
                    builder.AppendLine(indent + "    sparsityMetadataPresent=" + input.Sparsity.Present);
                    if (input.Sparsity.Present)
                    {
                        builder.AppendLine(indent + "    traversalOrder=[" + string.Join(",", input.Sparsity.TraversalOrder) + "]");
                        builder.AppendLine(indent + "    blockMap=[" + string.Join(",", input.Sparsity.BlockMap) + "]");
                        builder.AppendLine(indent + "    sparseIndexMetadataValueBytes~=" + input.Sparsity.MetadataValueBytes);
                        for (var dimensionIndex = 0; dimensionIndex < input.Sparsity.Dimensions.Length; dimensionIndex++)
                        {
                            var dimension = input.Sparsity.Dimensions[dimensionIndex];
                            builder.AppendLine(
                                indent + "    dim[" + dimensionIndex + "] format=" + dimension.Format +
                                " denseSize=" + dimension.DenseSize);
                            builder.AppendLine(indent + "      arraySegments: " + dimension.Segments);
                            builder.AppendLine(indent + "      arrayIndices: " + dimension.Indices);
                        }
                    }
                }

                for (var i = 0; i < Outputs.Length; i++)
                {
                    var output = Outputs[i];
                    builder.AppendLine(indent + "  output[" + i + "]: " + output.Tensor);
                    builder.AppendLine(indent + "    estimatedDenseValueBytes=" + output.EstimatedDenseBytes);
                    builder.AppendLine(indent + "    consumers=" +
                                       (output.Consumers.Length == 0 ? "[]" : "[" + string.Join(", ", output.Consumers) + "]"));
                }

                builder.AppendLine(indent + "  staticWrtModelInput=" + StaticWrtModelInput);
                builder.AppendLine(indent + "  outputsOnlyOrdinaryBuiltinConsumers=" + OnlyOrdinaryBuiltinConsumers +
                                   " (dataflow classification only; not a Sentis support verdict)");
                builder.AppendLine(indent + "  sparseStoredValueBytes=" + SparseValueBytes);
                builder.AppendLine(indent + "  sparseIndexMetadataValueBytes~=" + SparseMetadataValueBytes);
                builder.AppendLine(indent + "  estimatedDenseValueBytes=" + EstimatedDenseBytes);
                return builder.ToString();
            }
        }

    }
}
#endif
