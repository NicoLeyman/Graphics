using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEditor.Rendering;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph
{
    [Title("Input", "Texture", SampleVirtualTextureNode.DefaultNodeTitle)]
    class SampleVirtualTextureNode : AbstractMaterialNode, IGeneratesBodyCode, IMayRequireMeshUV, IHasSettings, IMayRequireTime
    {
        public const string DefaultNodeTitle = "Sample Virtual Texture";

        // input slots
        public const int UVInputId = 0;
        public const int VirtualTextureInputId = 1;
        public const int LODInputId = 2;
        public const int BiasInputId = 3;
        public const int DxInputId = 4;
        public const int DyInputId = 5;

        // output slots
        [NonSerialized]
        public readonly int[] OutputSlotIds = new int[] { 11, 12, 13, 14 };

        const string UVInputName = "UV";
        const string VirtualTextureInputName = "VT";
        const string LODSlotName = "Lod";
        const string BiasSlotName = "Bias";
        const string DxSlotName = "Dx";
        const string DySlotName = "Dy";

        static string[] OutputSlotNames = { "Out", "Out2", "Out3", "Out4" };

        public override bool hasPreview { get { return false; } }

        // Keep these in sync with "VirtualTexturing.hlsl"
        public enum LodCalculation
        {
            [InspectorName("Automatic")]
            VtLevel_Automatic = 0,
            [InspectorName("Lod Level")]
            VtLevel_Lod = 1,
            [InspectorName("Lod Bias")]
            VtLevel_Bias = 2,
            [InspectorName("Derivatives")]
            VtLevel_Derivatives = 3
        }

        public enum AddresMode
        {
            [InspectorName("Wrap")]
            VtAddressMode_Wrap = 0,
            [InspectorName("Clamp")]
            VtAddressMode_Clamp = 1,
            [InspectorName("Udim")]
            VtAddressMode_Udim = 2
        }

        public enum FilterMode
        {
            [InspectorName("Anisotropic")]
            VtFilter_Anisotropic = 0
        }

        public enum UvSpace
        {
            [InspectorName("Regular")]
            VtUvSpace_Regular = 0,
            [InspectorName("Pre Transformed")]
            VtUvSpace_PreTransformed = 1
        }

        public enum QualityMode
        {
            [InspectorName("Low")]
            VtSampleQuality_Low = 0,
            [InspectorName("High")]
            VtSampleQuality_High = 1
        }

        [SerializeField]
        LodCalculation m_LodCalculation = LodCalculation.VtLevel_Automatic;
        public LodCalculation lodCalculation
        {
            get
            {
                return m_LodCalculation;
            }
            set
            {
                if (m_LodCalculation == value)
                    return;

                m_LodCalculation = value;
                UpdateNodeAfterDeserialization();
                Dirty(ModificationScope.Topological);
            }
        }

        [SerializeField]
        QualityMode m_SampleQuality = QualityMode.VtSampleQuality_High;
        public QualityMode sampleQuality
        {
            get
            {
                return m_SampleQuality;
            }
            set
            {
                if (m_SampleQuality == value)
                    return;

                m_SampleQuality = value;
                Dirty(ModificationScope.Node);
            }
        }

        [SerializeField]
        bool m_NoFeedback;
        public bool noFeedback
        {
            get
            {
                return m_NoFeedback;
            }
            set
            {
                if (m_NoFeedback == value)
                    return;

                // No resolve affects the availability in the vertex shader of the node so we need to trigger a full
                // topo change.
                m_NoFeedback = value;
                UpdateNodeAfterDeserialization();
                Dirty(ModificationScope.Topological);
            }
        }

        [SerializeField]
        protected TextureType[] m_TextureTypes = { TextureType.Default, TextureType.Default, TextureType.Default, TextureType.Default };

        // We have one normal/object space field for all layers for now, probably a nice compromise
        // between lots of settings and user flexibility?
        [SerializeField]
        private NormalMapSpace m_NormalMapSpace = NormalMapSpace.Tangent;
        public NormalMapSpace normalMapSpace
        {
            get { return m_NormalMapSpace; }
            set
            {
                if (m_NormalMapSpace == value)
                    return;

                m_NormalMapSpace = value;
                Dirty(ModificationScope.Graph);
            }
        }

        /*
            True if the masternode of the graph this node is currently in supports virtual texturing.
        */ 
        private bool supportedByMasterNode      // TODO: 
        {
            get
            {
                var masterNode = owner?.GetNodes<IMasterNode>().FirstOrDefault();
                return masterNode?.virtualTexturingEnabled ?? false;
            }
        }

        /*
            The panel behind the cogwheel node settings
        */
        class SampleVirtualTextureNodeSettingsView : VisualElement
        {
            SampleVirtualTextureNode m_Node;
            public SampleVirtualTextureNodeSettingsView(SampleVirtualTextureNode node)
            {
                m_Node = node;

                PropertySheet ps = new PropertySheet();

                ps.Add(new PropertyRow(new Label("Lod Mode")), (row) =>
                {
                    row.Add(new UIElements.EnumField(m_Node.lodCalculation), (field) =>
                    {
                        field.value = m_Node.lodCalculation;
                        field.RegisterValueChangedCallback(evt =>
                        {
                            if (m_Node.lodCalculation == (LodCalculation)evt.newValue)
                                return;

                            m_Node.owner.owner.RegisterCompleteObjectUndo("Lod Mode Change");
                            m_Node.lodCalculation = (LodCalculation)evt.newValue;
                        });
                    });
                });

                ps.Add(new PropertyRow(new Label("Quality")), (row) =>
                {
                    row.Add(new UIElements.EnumField(m_Node.sampleQuality), (field) =>
                    {
                        field.value = m_Node.sampleQuality;
                        field.RegisterValueChangedCallback(evt =>
                        {
                            if (m_Node.sampleQuality == (QualityMode)evt.newValue)
                                return;

                            m_Node.owner.owner.RegisterCompleteObjectUndo("Quality Change");
                            m_Node.sampleQuality = (QualityMode)evt.newValue;
                        });
                    });
                });

                ps.Add(new PropertyRow(new Label("No Feedback")), (row) =>
                {
                    row.Add(new UnityEngine.UIElements.Toggle(), (field) =>
                    {
                        field.value = m_Node.noFeedback;
                        field.RegisterValueChangedCallback(evt =>
                        {
                            if (m_Node.noFeedback == evt.newValue)
                                return;

                            m_Node.owner.owner.RegisterCompleteObjectUndo("Feedback Settings Change");
                            m_Node.noFeedback = evt.newValue;
                        });
                    });
                });

                // TODO
//                 for (int i = 0; i < node.numSlots; i++)
//                 {
//                     int currentIndex = i; //to make lambda by-ref capturing happy
//                     ps.Add(new PropertyRow(new Label("Layer " + (i + 1) + " Type")), (row) =>
//                     {
//                         row.Add(new UIElements.EnumField(m_Node.m_TextureTypes[i]), (field) =>
//                         {
//                             field.value = m_Node.m_TextureTypes[i];
//                             field.RegisterValueChangedCallback(evt =>
//                             {
//                                 if (m_Node.m_TextureTypes[currentIndex] == (TextureType)evt.newValue)
//                                     return;
// 
//                                 m_Node.owner.owner.RegisterCompleteObjectUndo("Texture Type Change");
//                                 m_Node.m_TextureTypes[currentIndex] = (TextureType)evt.newValue;
//                                 m_Node.Dirty(ModificationScope.Graph);
//                             });
//                         });
//                     });
//                 }

                ps.Add(new PropertyRow(new Label("Normal Space")), (row) =>
                {
                    row.Add(new UIElements.EnumField(m_Node.normalMapSpace), (field) =>
                    {
                        field.value = m_Node.normalMapSpace;
                        field.RegisterValueChangedCallback(evt =>
                        {
                            if (m_Node.normalMapSpace == (NormalMapSpace)evt.newValue)
                                return;

                            m_Node.owner.owner.RegisterCompleteObjectUndo("Normal Map space Change");
                            m_Node.normalMapSpace = (NormalMapSpace)evt.newValue;
                        });
                    });
                });

#if !ENABLE_VIRTUALTEXTURES
                ps.Add(new HelpBoxRow(MessageType.Warning), (row) => row.Add(new Label("VT is disabled, this node will do regular 2D sampling.")));
#endif
                if (!m_Node.supportedByMasterNode)
                {
                    ps.Add(new HelpBoxRow(MessageType.Warning), (row) => row.Add(new Label("The current master node does not support VT, this node will do regular 2D sampling.")));
                }

                IVirtualTexturingEnabledRenderPipeline vtRp = GraphicsSettings.currentRenderPipeline as IVirtualTexturingEnabledRenderPipeline;
                if (vtRp == null || vtRp.virtualTexturingEnabled == false)
                {
                    ps.Add(new HelpBoxRow(MessageType.Warning), (row) => row.Add(new Label("The current render pipeline does not support VT." + ((vtRp ==null) ? "(Interface not implemented by" + GraphicsSettings.currentRenderPipeline.GetType().Name + ")" : "(virtualTexturingEnabled == false)"))));
                }

                Add(ps);
            }
        }

        public VisualElement CreateSettingsElement()
        {
            return new SampleVirtualTextureNodeSettingsView(this);
        }

        public SampleVirtualTextureNode() : this(false, false)
        { }

        public SampleVirtualTextureNode(bool isLod = false, bool noResolve = false)
        {
            name = "Sample Virtual Texture";
            UpdateNodeAfterDeserialization();
        }

        public override void UpdateNodeAfterDeserialization()
        {
            List<int> usedSlots = new List<int>();

            AddSlot(new UVMaterialSlot(UVInputId, UVInputName, UVInputName, UVChannel.UV0));
            usedSlots.Add(UVInputId);

            AddSlot(new VirtualTextureInputMaterialSlot(VirtualTextureInputId, VirtualTextureInputName, VirtualTextureInputName));
            usedSlots.Add(VirtualTextureInputId);

            // output slots
            for (int i = 0; i < 4; i++)
            {
                AddSlot(new Vector4MaterialSlot(OutputSlotIds[i], OutputSlotNames[i], OutputSlotNames[i], SlotType.Output, Vector4.zero, (noFeedback && m_LodCalculation == LodCalculation.VtLevel_Lod) ? ShaderStageCapability.All : ShaderStageCapability.Fragment));
                usedSlots.Add(OutputSlotIds[i]);
            }

            // Create slots

            if (m_LodCalculation == LodCalculation.VtLevel_Lod)
            {
                var slot = new Vector1MaterialSlot(LODInputId, LODSlotName, LODSlotName, SlotType.Input, 0.0f, ShaderStageCapability.All, LODSlotName);
                AddSlot(slot);
                usedSlots.Add(LODInputId);
            }

            if (m_LodCalculation == LodCalculation.VtLevel_Bias)
            {
                var slot = new Vector1MaterialSlot(BiasInputId, BiasSlotName, BiasSlotName, SlotType.Input, 0.0f, ShaderStageCapability.Fragment, BiasSlotName);
                AddSlot(slot);
                usedSlots.Add(BiasInputId);
            }

            if (m_LodCalculation == LodCalculation.VtLevel_Derivatives)
            {
                var slot1 = new Vector2MaterialSlot(DxInputId, DxSlotName, DxSlotName, SlotType.Input, Vector2.one, ShaderStageCapability.All, DxSlotName);
                var slot2 = new Vector2MaterialSlot(DyInputId, DySlotName, DySlotName, SlotType.Input, Vector2.one, ShaderStageCapability.All, DySlotName);
                AddSlot(slot1);
                AddSlot(slot2);
                usedSlots.Add(DxInputId);
                usedSlots.Add(DyInputId);
            }

            RemoveSlotsNameNotMatching(usedSlots, true);
        }

        public override void ValidateNode()
        {
            base.ValidateNode();
        }

        public string GetFeedbackVariableName()
        {
            return GetVariableNameForNode() + "_fb";
        }

        string MakeVtParameters(string variableName, string uvExpr, string lodExpr, string dxExpr, string dyExpr, AddresMode address, FilterMode filter, LodCalculation lod, UvSpace space, QualityMode quality)
        {
            const string VTParametersInputTemplate = @"
                        VtInputParameters {0};
                        {0}.uv = {1};
                        {0}.lodOrOffset = {2};
                        {0}.dx = {3};
                        {0}.dy = {4};
                        {0}.addressMode = {5};
                        {0}.filterMode = {6};
                        {0}.levelMode = {7};
                        {0}.uvMode = {8};
                        {0}.sampleQuality = {9};
#if defined(SHADER_STAGE_RAY_TRACING)
                        if ({0}.levelMode == VtLevel_Automatic || {0}.levelMode == VtLevel_Bias)
                        {{
                            {0}.levelMode = VtLevel_Lod;
                            {0}.lodOrOffset = 0.0f;
                        }}
#endif
            ";

            return string.Format(VTParametersInputTemplate,
                variableName,
                uvExpr,
                (string.IsNullOrEmpty(lodExpr)) ? "0.0f" : lodExpr,
                (string.IsNullOrEmpty(dxExpr)) ? "float2(0.0f, 0.0f)" : dxExpr,
                (string.IsNullOrEmpty(dyExpr)) ? "float2(0.0f, 0.0f)" : dyExpr,
                address.ToString(),
                filter.ToString(),
                lod.ToString(),
                space.ToString(),
                quality.ToString());
        }

        string MakeVtSample(string infoVariable, string layerName, string outputVariableName, LodCalculation lod, QualityMode quality)
        {
            const string SampleTemplate = @"$precision4 {0} = SampleStack({1}, {2}, {3}, {4});";

            return string.Format(SampleTemplate,
                outputVariableName,
                infoVariable,
                lod.ToString(),
                quality.ToString(),
                layerName);
        }

        // Node generations
        public virtual void GenerateNodeCode(ShaderStringBuilder sb, GenerationMode generationMode)
        {
            // Not all outputs may be connected (well one is or we wouldn't get called) so we are careful to
            // only generate code for connected outputs

            if (IsSlotConnected(VirtualTextureInputId))
            {
                var vtProperty = GetSlotProperty(VirtualTextureInputId) as VirtualTextureShaderProperty;
                int numSlots = vtProperty.value.entries.Count;
                string stackName = vtProperty.referenceName;        // I think the reference name is the stack name?

                string localVariablePrefix = GetVariableNameForNode();
                string parametersVariableNme = localVariablePrefix + "_pars";
                string infoVariableName = localVariablePrefix + "_info";

                bool anyConnected = false;
                for (int i = 0; i < numSlots; i++)
                {
                    if (IsSlotConnected(OutputSlotIds[i]))
                    {
                        anyConnected = true;
                        break;
                    }
                }

                if (anyConnected)
                {
                    sb.Append(MakeVtParameters(
                        parametersVariableNme,
                        GetSlotValue(UVInputId, generationMode),
                        (lodCalculation == LodCalculation.VtLevel_Lod) ? GetSlotValue(LODInputId, generationMode) : GetSlotValue(BiasInputId, generationMode),
                        GetSlotValue(DxInputId, generationMode),
                        GetSlotValue(DyInputId, generationMode),
                        AddresMode.VtAddressMode_Wrap,
                        FilterMode.VtFilter_Anisotropic,
                        m_LodCalculation,
                        UvSpace.VtUvSpace_Regular,
                        m_SampleQuality));

                    sb.AppendLine(string.Format("StackInfo {0} = PrepareStack({1}, {2});"
                                            , infoVariableName
                                            , parametersVariableNme
                                            , stackName));
                }

                for (int i = 0; i < numSlots; i++)
                {
                    if (IsSlotConnected(OutputSlotIds[i]))
                    {
                        var layerName = vtProperty.value.entries[i].layerName;
                        sb.AppendLine(MakeVtSample(infoVariableName, layerName, GetVariableNameForSlot(OutputSlotIds[i]), m_LodCalculation, m_SampleQuality));
                    }
                }

                for (int i = 0; i < numSlots; i++)
                {
                    if (IsSlotConnected(OutputSlotIds[i]))
                    {

                        if (m_TextureTypes[i] == TextureType.Normal)
                        {
                            if (normalMapSpace == NormalMapSpace.Tangent)
                            {
                                sb.AppendLine(string.Format("{0}.rgb = UnpackNormalmapRGorAG({0});", GetVariableNameForSlot(OutputSlotIds[i])));
                            }
                            else
                            {
                                sb.AppendLine(string.Format("{0}.rgb = UnpackNormalRGB({0});", GetVariableNameForSlot(OutputSlotIds[i])));
                            }
                        }
                    }
                }

                if (!noFeedback)
                {
                    //TODO: Investigate if the feedback pass can use halfs
                    string feedBackCode = string.Format("float4 {0} = GetResolveOutput({1});",
                            GetFeedbackVariableName(),
                            infoVariableName);
                    sb.AppendLine(feedBackCode);
                }
            }
        }

        public override void CollectShaderProperties(PropertyCollector properties, GenerationMode generationMode)
        {
            // this adds default properties for all of our unconnected inputs
            base.CollectShaderProperties(properties, generationMode);
        }

        public bool RequiresMeshUV(Internal.UVChannel channel, ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var slot in tempSlots)
                {
                    if (slot.RequiresMeshUV(channel))
                        return true;
                }
                return false;
            }
        }

        public bool RequiresTime()
        {
            return true;        // HACK: This ensures we repaint in shadergraph so data that gets streamed in also becomes visible.
        }
    }
}
