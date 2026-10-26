using System.Runtime.CompilerServices;

namespace PrismNRI.Native;

public partial struct DeviceDesc
{
    [NativeTypeName("nri::AdapterDesc")]
    public AdapterDesc adapterDesc;

    [NativeTypeName("nri::GraphicsAPI")]
    public GraphicsAPI graphicsAPI;

    [NativeTypeName("uint16_t")]
    public ushort nriVersion;

    [NativeTypeName("uint16_t")]
    public ushort shaderModel;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2005_C5")]
    public _viewport_e__Struct viewport;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2012_C5")]
    public _dimensions_e__Struct dimensions;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2023_C5")]
    public _precision_e__Struct precision;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2031_C5")]
    public _memory_e__Struct memory;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2045_C5")]
    public _memoryAlignment_e__Struct memoryAlignment;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2059_C5")]
    public _pipelineLayout_e__Struct pipelineLayout;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2067_C5")]
    public _descriptorSet_e__Struct descriptorSet;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2084_C5")]
    public _descriptorHeap_e__Struct descriptorHeap;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2093_C5")]
    public _shaderStage_e__Struct shaderStage;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2198_C5")]
    public _accelerationStructure_e__Struct accelerationStructure;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2208_C5")]
    public _wave_e__Struct wave;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2217_C5")]
    public _resolve_e__Struct resolve;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2225_C5")]
    public _other_e__Struct other;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2243_C5")]
    public _tiers_e__Struct tiers;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2288_C5")]
    public _features_e__Struct features;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2369_C5")]
    public _shaderFeatures_e__Struct shaderFeatures;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L2417_C5")]
    public _videoFeatures_e__Struct videoFeatures;

    public partial struct _viewport_e__Struct
    {
        [NativeTypeName("uint32_t")]
        public uint maxNum;

        [NativeTypeName("int32_t")]
        public int boundsMin;

        [NativeTypeName("int32_t")]
        public int boundsMax;
    }

    public partial struct _dimensions_e__Struct
    {
        [NativeTypeName("uint32_t")]
        public uint typedBufferMaxDim;

        [NativeTypeName("nri::Dim_t")]
        public ushort attachmentMaxDim;

        [NativeTypeName("nri::Dim_t")]
        public ushort attachmentLayerMaxNum;

        [NativeTypeName("nri::Dim_t")]
        public ushort texture1DMaxDim;

        [NativeTypeName("nri::Dim_t")]
        public ushort texture2DMaxDim;

        [NativeTypeName("nri::Dim_t")]
        public ushort texture3DMaxDim;

        [NativeTypeName("nri::Dim_t")]
        public ushort textureLayerMaxNum;
    }

    public partial struct _precision_e__Struct
    {
        [NativeTypeName("uint32_t")]
        public uint viewportBits;

        [NativeTypeName("uint32_t")]
        public uint subPixelBits;

        [NativeTypeName("uint32_t")]
        public uint subTexelBits;

        [NativeTypeName("uint32_t")]
        public uint mipmapBits;
    }

    public partial struct _memory_e__Struct
    {
        [NativeTypeName("uint64_t")]
        public ulong deviceUploadHeapSize;

        [NativeTypeName("uint64_t")]
        public ulong bufferMaxSize;

        [NativeTypeName("uint64_t")]
        public ulong allocationMaxSize;

        [NativeTypeName("uint32_t")]
        public uint allocationMaxNum;

        [NativeTypeName("uint32_t")]
        public uint samplerAllocationMaxNum;

        [NativeTypeName("uint32_t")]
        public uint constantBufferMaxRange;

        [NativeTypeName("uint32_t")]
        public uint storageBufferMaxRange;

        [NativeTypeName("uint32_t")]
        public uint bufferTextureGranularity;

        [NativeTypeName("uint32_t")]
        public uint alignmentDefault;

        [NativeTypeName("uint32_t")]
        public uint alignmentMultisample;
    }

    public partial struct _memoryAlignment_e__Struct
    {
        [NativeTypeName("uint32_t")]
        public uint uploadBufferTextureRow;

        [NativeTypeName("uint32_t")]
        public uint uploadBufferTextureSlice;

        [NativeTypeName("uint32_t")]
        public uint bufferShaderResourceOffset;

        [NativeTypeName("uint32_t")]
        public uint constantBufferOffset;

        [NativeTypeName("uint32_t")]
        public uint scratchBufferOffset;

        [NativeTypeName("uint32_t")]
        public uint shaderBindingTable;

        [NativeTypeName("uint32_t")]
        public uint accelerationStructureOffset;

        [NativeTypeName("uint32_t")]
        public uint micromapOffset;
    }

    public partial struct _pipelineLayout_e__Struct
    {
        [NativeTypeName("uint32_t")]
        public uint descriptorSetMaxNum;

        [NativeTypeName("uint32_t")]
        public uint rootConstantMaxSize;

        [NativeTypeName("uint32_t")]
        public uint rootDescriptorMaxNum;

        [NativeTypeName("uint32_t")]
        public uint rootSamplerMaxNum;
    }

    public partial struct _descriptorSet_e__Struct
    {
        [NativeTypeName("uint32_t")]
        public uint samplerMaxNum;

        [NativeTypeName("uint32_t")]
        public uint constantBufferMaxNum;

        [NativeTypeName("uint32_t")]
        public uint storageBufferMaxNum;

        [NativeTypeName("uint32_t")]
        public uint textureMaxNum;

        [NativeTypeName("uint32_t")]
        public uint storageTextureMaxNum;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2074_C9")]
        public _updateAfterSet_e__Struct updateAfterSet;

        public partial struct _updateAfterSet_e__Struct
        {
            [NativeTypeName("uint32_t")]
            public uint samplerMaxNum;

            [NativeTypeName("uint32_t")]
            public uint constantBufferMaxNum;

            [NativeTypeName("uint32_t")]
            public uint storageBufferMaxNum;

            [NativeTypeName("uint32_t")]
            public uint textureMaxNum;

            [NativeTypeName("uint32_t")]
            public uint storageTextureMaxNum;
        }
    }

    public partial struct _descriptorHeap_e__Struct
    {
        [NativeTypeName("uint32_t")]
        public uint resourceMaxNum;

        [NativeTypeName("uint32_t")]
        public uint samplerMaxNum;

        [NativeTypeName("uint32_t")]
        public uint rootConstantMaxSize;

        [NativeTypeName("uint32_t")]
        public uint rootDescriptorMaxNum;

        [NativeTypeName("uint32_t")]
        public uint rootSamplerMaxNum;
    }

    public partial struct _shaderStage_e__Struct
    {
        [NativeTypeName("uint32_t")]
        public uint descriptorSamplerMaxNum;

        [NativeTypeName("uint32_t")]
        public uint descriptorConstantBufferMaxNum;

        [NativeTypeName("uint32_t")]
        public uint descriptorStorageBufferMaxNum;

        [NativeTypeName("uint32_t")]
        public uint descriptorTextureMaxNum;

        [NativeTypeName("uint32_t")]
        public uint descriptorStorageTextureMaxNum;

        [NativeTypeName("uint32_t")]
        public uint resourceMaxNum;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2102_C9")]
        public _updateAfterSet_e__Struct updateAfterSet;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2112_C9")]
        public _vertex_e__Struct vertex;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2119_C9")]
        public _tesselationControl_e__Struct tesselationControl;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2129_C9")]
        public _tesselationEvaluation_e__Struct tesselationEvaluation;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2135_C9")]
        public _geometry_e__Struct geometry;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2144_C9")]
        public _fragment_e__Struct fragment;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2160_C9")]
        public _compute_e__Struct compute;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2168_C9")]
        public _task_e__Struct task;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2178_C9")]
        public _mesh_e__Struct mesh;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2190_C9")]
        public _rayTracing_e__Struct rayTracing;

        public partial struct _updateAfterSet_e__Struct
        {
            [NativeTypeName("uint32_t")]
            public uint descriptorSamplerMaxNum;

            [NativeTypeName("uint32_t")]
            public uint descriptorConstantBufferMaxNum;

            [NativeTypeName("uint32_t")]
            public uint descriptorStorageBufferMaxNum;

            [NativeTypeName("uint32_t")]
            public uint descriptorTextureMaxNum;

            [NativeTypeName("uint32_t")]
            public uint descriptorStorageTextureMaxNum;

            [NativeTypeName("uint32_t")]
            public uint resourceMaxNum;
        }

        public partial struct _vertex_e__Struct
        {
            [NativeTypeName("uint32_t")]
            public uint attributeMaxNum;

            [NativeTypeName("uint32_t")]
            public uint streamMaxNum;

            [NativeTypeName("uint32_t")]
            public uint outputComponentMaxNum;
        }

        public partial struct _tesselationControl_e__Struct
        {
            public float generationMaxLevel;

            [NativeTypeName("uint32_t")]
            public uint patchPointMaxNum;

            [NativeTypeName("uint32_t")]
            public uint perVertexInputComponentMaxNum;

            [NativeTypeName("uint32_t")]
            public uint perVertexOutputComponentMaxNum;

            [NativeTypeName("uint32_t")]
            public uint perPatchOutputComponentMaxNum;

            [NativeTypeName("uint32_t")]
            public uint totalOutputComponentMaxNum;
        }

        public partial struct _tesselationEvaluation_e__Struct
        {
            [NativeTypeName("uint32_t")]
            public uint inputComponentMaxNum;

            [NativeTypeName("uint32_t")]
            public uint outputComponentMaxNum;
        }

        public partial struct _geometry_e__Struct
        {
            [NativeTypeName("uint32_t")]
            public uint invocationMaxNum;

            [NativeTypeName("uint32_t")]
            public uint inputComponentMaxNum;

            [NativeTypeName("uint32_t")]
            public uint outputComponentMaxNum;

            [NativeTypeName("uint32_t")]
            public uint outputVertexMaxNum;

            [NativeTypeName("uint32_t")]
            public uint totalOutputComponentMaxNum;
        }

        public partial struct _fragment_e__Struct
        {
            [NativeTypeName("uint32_t")]
            public uint inputComponentMaxNum;

            [NativeTypeName("uint32_t")]
            public uint attachmentMaxNum;

            [NativeTypeName("uint32_t")]
            public uint dualSourceAttachmentMaxNum;
        }

        public partial struct _compute_e__Struct
        {
            [NativeTypeName("uint32_t[3]")]
            public _dispatchMaxDim_e__FixedBuffer dispatchMaxDim;

            [NativeTypeName("uint32_t")]
            public uint workGroupInvocationMaxNum;

            [NativeTypeName("uint32_t[3]")]
            public _workGroupMaxDim_e__FixedBuffer workGroupMaxDim;

            [NativeTypeName("uint32_t")]
            public uint sharedMemoryMaxSize;

            [InlineArray(3)]
            public partial struct _dispatchMaxDim_e__FixedBuffer
            {
                public uint e0;
            }

            [InlineArray(3)]
            public partial struct _workGroupMaxDim_e__FixedBuffer
            {
                public uint e0;
            }
        }

        public partial struct _task_e__Struct
        {
            [NativeTypeName("uint32_t")]
            public uint dispatchWorkGroupMaxNum;

            [NativeTypeName("uint32_t[3]")]
            public _dispatchMaxDim_e__FixedBuffer dispatchMaxDim;

            [NativeTypeName("uint32_t")]
            public uint workGroupInvocationMaxNum;

            [NativeTypeName("uint32_t[3]")]
            public _workGroupMaxDim_e__FixedBuffer workGroupMaxDim;

            [NativeTypeName("uint32_t")]
            public uint sharedMemoryMaxSize;

            [NativeTypeName("uint32_t")]
            public uint payloadMaxSize;

            [InlineArray(3)]
            public partial struct _dispatchMaxDim_e__FixedBuffer
            {
                public uint e0;
            }

            [InlineArray(3)]
            public partial struct _workGroupMaxDim_e__FixedBuffer
            {
                public uint e0;
            }
        }

        public partial struct _mesh_e__Struct
        {
            [NativeTypeName("uint32_t")]
            public uint dispatchWorkGroupMaxNum;

            [NativeTypeName("uint32_t[3]")]
            public _dispatchMaxDim_e__FixedBuffer dispatchMaxDim;

            [NativeTypeName("uint32_t")]
            public uint workGroupInvocationMaxNum;

            [NativeTypeName("uint32_t[3]")]
            public _workGroupMaxDim_e__FixedBuffer workGroupMaxDim;

            [NativeTypeName("uint32_t")]
            public uint sharedMemoryMaxSize;

            [NativeTypeName("uint32_t")]
            public uint outputVerticesMaxNum;

            [NativeTypeName("uint32_t")]
            public uint outputPrimitiveMaxNum;

            [NativeTypeName("uint32_t")]
            public uint outputComponentMaxNum;

            [InlineArray(3)]
            public partial struct _dispatchMaxDim_e__FixedBuffer
            {
                public uint e0;
            }

            [InlineArray(3)]
            public partial struct _workGroupMaxDim_e__FixedBuffer
            {
                public uint e0;
            }
        }

        public partial struct _rayTracing_e__Struct
        {
            [NativeTypeName("uint32_t")]
            public uint shaderGroupIdentifierSize;

            [NativeTypeName("uint32_t")]
            public uint shaderBindingTableMaxStride;

            [NativeTypeName("uint32_t")]
            public uint recursionMaxDepth;
        }
    }

    public partial struct _accelerationStructure_e__Struct
    {
        [NativeTypeName("uint64_t")]
        public ulong primitiveMaxNum;

        [NativeTypeName("uint64_t")]
        public ulong geometryMaxNum;

        [NativeTypeName("uint64_t")]
        public ulong instanceMaxNum;

        [NativeTypeName("uint32_t")]
        public uint micromapSubdivisionMaxLevel;
    }

    public partial struct _wave_e__Struct
    {
        [NativeTypeName("uint32_t")]
        public uint laneMinNum;

        [NativeTypeName("uint32_t")]
        public uint laneMaxNum;

        [NativeTypeName("nri::StageBits")]
        public StageBits waveOpsStages;

        [NativeTypeName("nri::StageBits")]
        public StageBits quadOpsStages;

        [NativeTypeName("nri::StageBits")]
        public StageBits derivativeOpsStages;
    }

    public partial struct _resolve_e__Struct
    {
        [NativeTypeName("nri::ResolveOps")]
        public ResolveOps attachment;

        [NativeTypeName("nri::ResolveOps")]
        public ResolveOps command;

        [NativeTypeName("bool")]
        public byte independentDepthStencil;

        [NativeTypeName("bool")]
        public byte independentDepthStencilNone;
    }

    public partial struct _other_e__Struct
    {
        [NativeTypeName("uint64_t")]
        public ulong timestampFrequencyHz;

        [NativeTypeName("uint32_t")]
        public uint drawIndirectMaxNum;

        public float samplerLodBiasMax;

        public float samplerAnisotropyMax;

        [NativeTypeName("int8_t")]
        public sbyte texelGatherOffsetMin;

        [NativeTypeName("int8_t")]
        public sbyte texelOffsetMin;

        [NativeTypeName("uint8_t")]
        public byte texelOffsetMax;

        [NativeTypeName("uint8_t")]
        public byte texelGatherOffsetMax;

        [NativeTypeName("uint8_t")]
        public byte clipDistanceMaxNum;

        [NativeTypeName("uint8_t")]
        public byte cullDistanceMaxNum;

        [NativeTypeName("uint8_t")]
        public byte combinedClipAndCullDistanceMaxNum;

        [NativeTypeName("uint8_t")]
        public byte viewMaxNum;

        [NativeTypeName("uint8_t")]
        public byte shadingRateAttachmentTileSize;

        [NativeTypeName("bool")]
        public byte timestampCopyQueueResolveOnCopyQueue;
    }

    public partial struct _tiers_e__Struct
    {
        [NativeTypeName("uint8_t")]
        public byte conservativeRaster;

        [NativeTypeName("uint8_t")]
        public byte sampleLocations;

        [NativeTypeName("uint8_t")]
        public byte rayTracing;

        [NativeTypeName("uint8_t")]
        public byte shadingRate;

        [NativeTypeName("uint8_t")]
        public byte resourceBinding;

        [NativeTypeName("uint8_t")]
        public byte bindless;

        [NativeTypeName("uint8_t")]
        public byte memory;
    }

    public partial struct _features_e__Struct
    {
        [NativeTypeName("bool")]
        public byte swapChain;

        [NativeTypeName("bool")]
        public byte presentFromCompute;

        [NativeTypeName("bool")]
        public byte waitableSwapChain;

        [NativeTypeName("bool")]
        public byte resizableSwapChain;

        [NativeTypeName("bool")]
        public byte latestReadySwapChain;

        [NativeTypeName("bool")]
        public byte flexibleMultiview;

        [NativeTypeName("bool")]
        public byte layerBasedMultiview;

        [NativeTypeName("bool")]
        public byte viewportBasedMultiview;

        [NativeTypeName("bool")]
        public byte sharedResources;

        [NativeTypeName("bool")]
        public byte sharedFences;

        [NativeTypeName("bool")]
        public byte textureCompressionBC;

        [NativeTypeName("bool")]
        public byte textureCompressionETC2;

        [NativeTypeName("bool")]
        public byte textureCompressionASTC;

        [NativeTypeName("bool")]
        public byte shaderBytecodeDXBC;

        [NativeTypeName("bool")]
        public byte shaderBytecodeDXIL;

        [NativeTypeName("bool")]
        public byte shaderBytecodeSPIRV;

        [NativeTypeName("bool")]
        public byte shaderBytecodeWGSL;

        [NativeTypeName("bool")]
        public byte shaderBytecodeMETALLIB;

        [NativeTypeName("bool")]
        public byte occlusion;

        [NativeTypeName("bool")]
        public byte timestamp;

        [NativeTypeName("bool")]
        public byte timestampCopyQueue;

        [NativeTypeName("bool")]
        public byte calibratedTimestamps;

        [NativeTypeName("bool")]
        public byte additionalShadingRates;

        [NativeTypeName("bool")]
        public byte sumShadingRateCombiner;

        [NativeTypeName("bool")]
        public byte rectColorClears;

        [NativeTypeName("bool")]
        public byte rectDepthStencilClears;

        [NativeTypeName("bool")]
        public byte regionResolve;

        [NativeTypeName("bool")]
        public byte pipelineCache;

        [NativeTypeName("bool")]
        public byte pipelineCacheControl;

        [NativeTypeName("bool")]
        public byte getMemoryDesc2;

        [NativeTypeName("bool")]
        public byte enhancedBarriers;

        [NativeTypeName("bool")]
        public byte tessellationShader;

        [NativeTypeName("bool")]
        public byte geometryShader;

        [NativeTypeName("bool")]
        public byte meshShader;

        [NativeTypeName("bool")]
        public byte lowLatency;

        [NativeTypeName("bool")]
        public byte descriptorHeap;

        [NativeTypeName("bool")]
        public byte video;

        [NativeTypeName("bool")]
        public byte componentSwizzle;

        [NativeTypeName("bool")]
        public byte independentFrontAndBackStencilReferenceAndMasks;

        [NativeTypeName("bool")]
        public byte filterOpMinMax;

        [NativeTypeName("bool")]
        public byte constantAlphaBlendFactors;

        [NativeTypeName("bool")]
        public byte logicOp;

        [NativeTypeName("bool")]
        public byte depthBoundsTest;

        [NativeTypeName("bool")]
        public byte drawIndirectCount;

        [NativeTypeName("bool")]
        public byte lineSmoothing;

        [NativeTypeName("bool")]
        public byte meshShaderPipelineStats;

        [NativeTypeName("bool")]
        public byte dynamicDepthBias;

        [NativeTypeName("bool")]
        public byte viewportOriginBottomLeft;

        [NativeTypeName("bool")]
        public byte pipelineStatistics;

        [NativeTypeName("bool")]
        public byte rootConstantsOffset;

        [NativeTypeName("bool")]
        public byte nonConstantBufferRootDescriptorOffset;

        [NativeTypeName("bool")]
        public byte mutableDescriptorType;

        [NativeTypeName("bool")]
        public byte extendedDynamicState;

        [NativeTypeName("bool")]
        public byte unifiedTextureLayouts;

        [NativeTypeName("bool")]
        public byte resourceAliasing;
    }

    public partial struct _shaderFeatures_e__Struct
    {
        [NativeTypeName("bool")]
        public byte nativeI8;

        [NativeTypeName("bool")]
        public byte nativeI16;

        [NativeTypeName("bool")]
        public byte nativeF16;

        [NativeTypeName("bool")]
        public byte nativeI64;

        [NativeTypeName("bool")]
        public byte nativeF64;

        [NativeTypeName("bool")]
        public byte atomicsI16;

        [NativeTypeName("bool")]
        public byte atomicsF16;

        [NativeTypeName("bool")]
        public byte atomicsF32;

        [NativeTypeName("bool")]
        public byte atomicsI64;

        [NativeTypeName("bool")]
        public byte atomicsF64;

        [NativeTypeName("bool")]
        public byte storageReadWithoutFormat;

        [NativeTypeName("bool")]
        public byte storageWriteWithoutFormat;

        [NativeTypeName("bool")]
        public byte waveQuery;

        [NativeTypeName("bool")]
        public byte waveVote;

        [NativeTypeName("bool")]
        public byte waveShuffle;

        [NativeTypeName("bool")]
        public byte waveArithmetic;

        [NativeTypeName("bool")]
        public byte waveReduction;

        [NativeTypeName("bool")]
        public byte waveQuad;

        [NativeTypeName("bool")]
        public byte viewportIndex;

        [NativeTypeName("bool")]
        public byte layerIndex;

        [NativeTypeName("bool")]
        public byte unnormalizedCoordinates;

        [NativeTypeName("bool")]
        public byte clock;

        [NativeTypeName("bool")]
        public byte rasterizedOrderedView;

        [NativeTypeName("bool")]
        public byte barycentric;

        [NativeTypeName("bool")]
        public byte rayTracingPositionFetch;

        [NativeTypeName("bool")]
        public byte integerDotProduct;

        [NativeTypeName("bool")]
        public byte inputAttachments;

        [NativeTypeName("bool")]
        public byte drawParameters;

        [NativeTypeName("bool")]
        public byte drawIndex;
    }

    public partial struct _videoFeatures_e__Struct
    {
        [NativeTypeName("__AnonymousRecord_NRIDescs_L2418_C9")]
        public _decode_e__Struct decode;

        [NativeTypeName("__AnonymousRecord_NRIDescs_L2424_C9")]
        public _encode_e__Struct encode;

        public partial struct _decode_e__Struct
        {
            [NativeTypeName("bool")]
            public byte H264;

            [NativeTypeName("bool")]
            public byte H265;

            [NativeTypeName("bool")]
            public byte AV1;
        }

        public partial struct _encode_e__Struct
        {
            [NativeTypeName("bool")]
            public byte H264;

            [NativeTypeName("bool")]
            public byte H265;

            [NativeTypeName("bool")]
            public byte AV1;
        }
    }
}
