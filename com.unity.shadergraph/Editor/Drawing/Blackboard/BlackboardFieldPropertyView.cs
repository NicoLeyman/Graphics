using System;
using System.Linq;
using System.Globalization;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Toggle = UnityEngine.UIElements.Toggle;
using UnityEditor.Experimental.GraphView;
using UnityEditor.ShaderGraph.Internal;
using UnityEditorInternal;
using System.Collections.Generic;

namespace UnityEditor.ShaderGraph.Drawing
{
    class BlackboardFieldPropertyView : BlackboardFieldView
    {
        private ReorderableList m_VTReorderableList; //reorderable list data for the Virtual Texture property
        private IMGUIContainer m_VTContainer;
        private int m_VTSelectedIndex;
        public BlackboardFieldPropertyView(BlackboardField blackboardField, GraphData graph, ShaderInput input)
            : base (blackboardField, graph, input)
        {
        }

        public override void BuildCustomFields(ShaderInput input)
        {
            AbstractShaderProperty property = input as AbstractShaderProperty;
            if(property == null)
                return;

            switch(input)
            {
                case Vector1ShaderProperty vector1Property:
                    BuildVector1PropertyField(vector1Property);
                    break;
                case Vector2ShaderProperty vector2Property:
                    BuildVector2PropertyField(vector2Property);
                    break;
                case Vector3ShaderProperty vector3Property:
                    BuildVector3PropertyField(vector3Property);
                    break;
                case Vector4ShaderProperty vector4Property:
                    BuildVector4PropertyField(vector4Property);
                    break;
                case ColorShaderProperty colorProperty:
                    BuildColorPropertyField(colorProperty);
                    break;
                case Texture2DShaderProperty texture2DProperty:
                    BuildTexture2DPropertyField(texture2DProperty);
                    break;
                case Texture2DArrayShaderProperty texture2DArrayProperty:
                    BuildTexture2DArrayPropertyField(texture2DArrayProperty);
                    break;
                case Texture3DShaderProperty texture3DProperty:
                    BuildTexture3DPropertyField(texture3DProperty);
                    break;
                case CubemapShaderProperty cubemapProperty:
                    BuildCubemapPropertyField(cubemapProperty);
                    break;
                case BooleanShaderProperty booleanProperty:
                    BuildBooleanPropertyField(booleanProperty);
                    break;
                case Matrix2ShaderProperty matrix2Property:
                    BuildMatrix2PropertyField(matrix2Property);
                    break;
                case Matrix3ShaderProperty matrix3Property:
                    BuildMatrix3PropertyField(matrix3Property);
                    break;
                case Matrix4ShaderProperty matrix4Property:
                    BuildMatrix4PropertyField(matrix4Property);
                    break;
                case SamplerStateShaderProperty samplerStateProperty:
                    BuildSamplerStatePropertyField(samplerStateProperty);
                    break;
                case GradientShaderProperty gradientProperty:
                    BuildGradientPropertyField(gradientProperty);
                    break;
                case VirtualTextureShaderProperty virtualTextureProperty:
                    BuildVirtualTexturePropertyField(virtualTextureProperty);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Precision
            var precisionField = new EnumField((Enum)property.precision);
            precisionField.RegisterValueChangedCallback(evt =>
            {
                graph.owner.RegisterCompleteObjectUndo("Change Precision");
                if (property.precision == (Precision)evt.newValue)
                    return;

                property.precision = (Precision)evt.newValue;
                graph.ValidateGraph();
                precisionField.MarkDirtyRepaint();
                DirtyNodes();
            });
            AddRow("Precision", precisionField);
            if (property.isGpuInstanceable)
            {
                Toggle gpuInstancedToogle = new Toggle { value = property.gpuInstanced };
                gpuInstancedToogle.OnToggleChanged(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Hybrid Instanced Toggle");
                    property.gpuInstanced = evt.newValue;
                    DirtyNodes(ModificationScope.Graph);
                });
                AddRow("Hybrid Instanced (experimental)", gpuInstancedToogle);
            }

        }

        void BuildVector1PropertyField(Vector1ShaderProperty property)
        {
            switch (property.floatType)
            {
                case FloatType.Slider:
                    {
                        float min = Mathf.Min(property.value, property.rangeValues.x);
                        float max = Mathf.Max(property.value, property.rangeValues.y);
                        property.rangeValues = new Vector2(min, max);

                        var defaultField = new FloatField { value = property.value };
                        var minField = new FloatField { value = property.rangeValues.x };
                        var maxField = new FloatField { value = property.rangeValues.y };

                        defaultField.RegisterValueChangedCallback(evt =>
                        {
                            property.value = (float)evt.newValue;
                            this.MarkDirtyRepaint();
                        });
                        defaultField.Q("unity-text-input").RegisterCallback<FocusOutEvent>(evt =>
                        {
                            graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                            float minValue = Mathf.Min(property.value, property.rangeValues.x);
                            float maxValue = Mathf.Max(property.value, property.rangeValues.y);
                            property.rangeValues = new Vector2(minValue, maxValue);
                            minField.value = minValue;
                            maxField.value = maxValue;
                            DirtyNodes();
                        });
                        minField.RegisterValueChangedCallback(evt =>
                        {
                            graph.owner.RegisterCompleteObjectUndo("Change Range Property Minimum");
                            property.rangeValues = new Vector2((float)evt.newValue, property.rangeValues.y);
                            DirtyNodes();
                        });
                        minField.Q("unity-text-input").RegisterCallback<FocusOutEvent>(evt =>
                        {
                            property.value = Mathf.Max(Mathf.Min(property.value, property.rangeValues.y), property.rangeValues.x);
                            defaultField.value = property.value;
                            DirtyNodes();
                        });
                        maxField.RegisterValueChangedCallback(evt =>
                        {
                            graph.owner.RegisterCompleteObjectUndo("Change Range Property Maximum");
                            property.rangeValues = new Vector2(property.rangeValues.x, (float)evt.newValue);
                            DirtyNodes();
                        });
                        maxField.Q("unity-text-input").RegisterCallback<FocusOutEvent>(evt =>
                        {
                            property.value = Mathf.Max(Mathf.Min(property.value, property.rangeValues.y), property.rangeValues.x);
                            defaultField.value = property.value;
                            DirtyNodes();
                        });

                        AddRow("Default", defaultField);
                        AddRow("Min", minField);
                        AddRow("Max", maxField);
                    }
                    break;
                case FloatType.Integer:
                    {
                        property.value = (int)property.value;
                        var defaultField = new IntegerField { value = (int)property.value };
                        defaultField.RegisterValueChangedCallback(evt =>
                        {
                            graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                            property.value = (int)evt.newValue;
                            DirtyNodes();
                        });
                        AddRow("Default", defaultField);
                    }
                    break;
                default:
                    {
                        var defaultField = new FloatField { value = property.value };
                        defaultField.RegisterValueChangedCallback(evt =>
                        {
                            graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                            property.value = (float)evt.newValue;
                            DirtyNodes();
                        });
                        AddRow("Default", defaultField);
                    }
                    break;
            }

            if(!graph.isSubGraph)
            {
                var modeField = new EnumField(property.floatType);
                modeField.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Vector1 Mode");
                    property.floatType = (FloatType)evt.newValue;
                    Rebuild();
                });
                AddRow("Mode", modeField);
            }
        }

        void BuildVector2PropertyField(Vector2ShaderProperty property)
        {
            var field = new Vector2Field { value = property.value };

            field.Q("unity-x-input").Q("unity-text-input").RegisterCallback<KeyDownEvent>(keyDownCallback);
            field.Q("unity-x-input").Q("unity-text-input").RegisterCallback<FocusOutEvent>(focusOutCallback);
            field.Q("unity-y-input").Q("unity-text-input").RegisterCallback<KeyDownEvent>(keyDownCallback);
            field.Q("unity-y-input").Q("unity-text-input").RegisterCallback<FocusOutEvent>(focusOutCallback);

            // Called after KeyDownEvent
            field.RegisterValueChangedCallback(evt =>
                {
                    // Only true when setting value via FieldMouseDragger
                    // Undo recorded once per dragger release
                    if (undoGroup == -1)
                        graph.owner.RegisterCompleteObjectUndo("Change property value");

                    property.value = evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", field);
        }

        void BuildVector3PropertyField(Vector3ShaderProperty property)
        {
            var field = new Vector3Field { value = property.value };

            field.Q("unity-x-input").Q("unity-text-input").RegisterCallback<KeyDownEvent>(keyDownCallback);
            field.Q("unity-x-input").Q("unity-text-input").RegisterCallback<FocusOutEvent>(focusOutCallback);
            field.Q("unity-y-input").Q("unity-text-input").RegisterCallback<KeyDownEvent>(keyDownCallback);
            field.Q("unity-y-input").Q("unity-text-input").RegisterCallback<FocusOutEvent>(focusOutCallback);
            field.Q("unity-z-input").Q("unity-text-input").RegisterCallback<KeyDownEvent>(keyDownCallback);
            field.Q("unity-z-input").Q("unity-text-input").RegisterCallback<FocusOutEvent>(focusOutCallback);

            // Called after KeyDownEvent
            field.RegisterValueChangedCallback(evt =>
                {
                    // Only true when setting value via FieldMouseDragger
                    // Undo recorded once per dragger release
                    if (undoGroup == -1)
                        graph.owner.RegisterCompleteObjectUndo("Change property value");

                    property.value = evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", field);
        }

        void BuildVector4PropertyField(Vector4ShaderProperty property)
        {
            var field = new Vector4Field { value = property.value };

            field.Q("unity-x-input").Q("unity-text-input").RegisterCallback<KeyDownEvent>(keyDownCallback);
            field.Q("unity-x-input").Q("unity-text-input").RegisterCallback<FocusOutEvent>(focusOutCallback);
            field.Q("unity-y-input").Q("unity-text-input").RegisterCallback<KeyDownEvent>(keyDownCallback);
            field.Q("unity-y-input").Q("unity-text-input").RegisterCallback<FocusOutEvent>(focusOutCallback);
            field.Q("unity-z-input").Q("unity-text-input").RegisterCallback<KeyDownEvent>(keyDownCallback);
            field.Q("unity-z-input").Q("unity-text-input").RegisterCallback<FocusOutEvent>(focusOutCallback);
            field.Q("unity-w-input").Q("unity-text-input").RegisterCallback<KeyDownEvent>(keyDownCallback);
            field.Q("unity-w-input").Q("unity-text-input").RegisterCallback<FocusOutEvent>(focusOutCallback);

            // Called after KeyDownEvent
            field.RegisterValueChangedCallback(evt =>
                {
                    // Only true when setting value via FieldMouseDragger
                    // Undo recorded once per dragger release
                    if (undoGroup == -1)
                        graph.owner.RegisterCompleteObjectUndo("Change property value");

                    property.value = evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", field);
        }

        void BuildColorPropertyField(ColorShaderProperty property)
        {
            var colorField = new ColorField { value = property.value, showEyeDropper = false, hdr = property.colorMode == ColorMode.HDR };
            colorField.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change property value");
                    property.value = evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", colorField);

            if(!graph.isSubGraph)
            {
                var colorModeField = new EnumField((Enum)property.colorMode);
                colorModeField.RegisterValueChangedCallback(evt =>
                    {
                        graph.owner.RegisterCompleteObjectUndo("Change Color Mode");
                        if (property.colorMode == (ColorMode)evt.newValue)
                            return;
                        property.colorMode = (ColorMode)evt.newValue;
                        colorField.hdr = property.colorMode == ColorMode.HDR;
                        colorField.MarkDirtyRepaint();
                        DirtyNodes();
                    });
                AddRow("Mode", colorModeField);
            }
        }

        void BuildTexture2DPropertyField(Texture2DShaderProperty property)
        {
            var field = new ObjectField { value = property.value.texture, objectType = typeof(Texture) };
            field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change property value");
                    property.value.texture = (Texture)evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", field);

            var defaultMode = (Enum)Texture2DShaderProperty.DefaultType.Grey;
            var textureMode = property.generatePropertyBlock ? (Enum)property.defaultType : defaultMode;
            var defaultModeField = new EnumField(textureMode);
                defaultModeField.RegisterValueChangedCallback(evt =>
                    {
                        graph.owner.RegisterCompleteObjectUndo("Change Texture Mode");
                        if (property.defaultType == (Texture2DShaderProperty.DefaultType)evt.newValue)
                            return;
                        property.defaultType = (Texture2DShaderProperty.DefaultType)evt.newValue;
                        DirtyNodes(ModificationScope.Graph);
                    });
            AddRow("Mode", defaultModeField, !graph.isSubGraph && property.generatePropertyBlock);
        }
        void BuildTexture2DArrayPropertyField(Texture2DArrayShaderProperty property)
        {
            var field = new ObjectField { value = property.value.textureArray, objectType = typeof(Texture2DArray) };
            field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change property value");
                    property.value.textureArray = (Texture2DArray)evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", field);
        }

        void BuildTexture3DPropertyField(Texture3DShaderProperty property)
        {
            var field = new ObjectField { value = property.value.texture, objectType = typeof(Texture3D) };
            field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change property value");
                    property.value.texture = (Texture3D)evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", field);
        }

        void BuildCubemapPropertyField(CubemapShaderProperty property)
        {
            var field = new ObjectField { value = property.value.cubemap, objectType = typeof(Cubemap) };
            field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change property value");
                    property.value.cubemap = (Cubemap)evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", field);
        }

        void BuildBooleanPropertyField(BooleanShaderProperty property)
        {
            var field = new Toggle() { value = property.value };
            field.OnToggleChanged(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change property value");
                    property.value = evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", field);
        }

        void BuildMatrix2PropertyField(Matrix2ShaderProperty property)
        {
            var row0Field = new Vector2Field { value = property.value.GetRow(0) };
            row0Field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                    Vector2 row1 = property.value.GetRow(1);
                    property.value = new Matrix4x4()
                    {
                        m00 = evt.newValue.x,
                        m01 = evt.newValue.y,
                        m02 = 0,
                        m03 = 0,
                        m10 = row1.x,
                        m11 = row1.y,
                        m12 = 0,
                        m13 = 0,
                        m20 = 0,
                        m21 = 0,
                        m22 = 0,
                        m23 = 0,
                        m30 = 0,
                        m31 = 0,
                        m32 = 0,
                        m33 = 0,
                    };
                    DirtyNodes();
                });
            AddRow("Default", row0Field);

            var row1Field = new Vector2Field { value = property.value.GetRow(1) };
            row1Field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                    Vector2 row0 = property.value.GetRow(0);
                    property.value = new Matrix4x4()
                    {
                        m00 = row0.x,
                        m01 = row0.y,
                        m02 = 0,
                        m03 = 0,
                        m10 = evt.newValue.x,
                        m11 = evt.newValue.y,
                        m12 = 0,
                        m13 = 0,
                        m20 = 0,
                        m21 = 0,
                        m22 = 0,
                        m23 = 0,
                        m30 = 0,
                        m31 = 0,
                        m32 = 0,
                        m33 = 0,
                    };
                    DirtyNodes();
                });
            AddRow("", row1Field);
        }

        void BuildMatrix3PropertyField(Matrix3ShaderProperty property)
        {
            var row0Field = new Vector3Field { value = property.value.GetRow(0) };
                row0Field.RegisterValueChangedCallback(evt =>
                    {
                        graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                        Vector3 row1 = property.value.GetRow(1);
                        Vector3 row2 = property.value.GetRow(2);
                        property.value = new Matrix4x4()
                        {
                            m00 = evt.newValue.x,
                            m01 = evt.newValue.y,
                            m02 = evt.newValue.z,
                            m03 = 0,
                            m10 = row1.x,
                            m11 = row1.y,
                            m12 = row1.z,
                            m13 = 0,
                            m20 = row2.x,
                            m21 = row2.y,
                            m22 = row2.z,
                            m23 = 0,
                            m30 = 0,
                            m31 = 0,
                            m32 = 0,
                            m33 = 0,
                        };
                        DirtyNodes();
                    });
                AddRow("Default", row0Field);

                var row1Field = new Vector3Field { value = property.value.GetRow(1) };
                row1Field.RegisterValueChangedCallback(evt =>
                    {
                        graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                        Vector3 row0 = property.value.GetRow(0);
                        Vector3 row2 = property.value.GetRow(2);
                        property.value = new Matrix4x4()
                        {
                            m00 = row0.x,
                            m01 = row0.y,
                            m02 = row0.z,
                            m03 = 0,
                            m10 = evt.newValue.x,
                            m11 = evt.newValue.y,
                            m12 = evt.newValue.z,
                            m13 = 0,
                            m20 = row2.x,
                            m21 = row2.y,
                            m22 = row2.z,
                            m23 = 0,
                            m30 = 0,
                            m31 = 0,
                            m32 = 0,
                            m33 = 0,
                        };
                        DirtyNodes();
                    });

                AddRow("", row1Field);
                var row2Field = new Vector3Field { value = property.value.GetRow(2) };
                row2Field.RegisterValueChangedCallback(evt =>
                    {
                        graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                        Vector3 row0 = property.value.GetRow(0);
                        Vector3 row1 = property.value.GetRow(1);
                        property.value = new Matrix4x4()
                        {
                            m00 = row0.x,
                            m01 = row0.y,
                            m02 = row0.z,
                            m03 = 0,
                            m10 = row1.x,
                            m11 = row1.y,
                            m12 = row1.z,
                            m13 = 0,
                            m20 = evt.newValue.x,
                            m21 = evt.newValue.y,
                            m22 = evt.newValue.z,
                            m23 = 0,
                            m30 = 0,
                            m31 = 0,
                            m32 = 0,
                            m33 = 0,
                        };
                        DirtyNodes();
                    });
                AddRow("", row2Field);
        }

        void BuildMatrix4PropertyField(Matrix4ShaderProperty property)
        {
            var row0Field = new Vector4Field { value = property.value.GetRow(0) };
            row0Field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                    Vector4 row1 = property.value.GetRow(1);
                    Vector4 row2 = property.value.GetRow(2);
                    Vector4 row3 = property.value.GetRow(3);
                    property.value = new Matrix4x4()
                    {
                        m00 = evt.newValue.x,
                        m01 = evt.newValue.y,
                        m02 = evt.newValue.z,
                        m03 = evt.newValue.w,
                        m10 = row1.x,
                        m11 = row1.y,
                        m12 = row1.z,
                        m13 = row1.w,
                        m20 = row2.x,
                        m21 = row2.y,
                        m22 = row2.z,
                        m23 = row2.w,
                        m30 = row3.x,
                        m31 = row3.y,
                        m32 = row3.z,
                        m33 = row3.w,
                    };
                    DirtyNodes();
                });
            AddRow("Default", row0Field);

            var row1Field = new Vector4Field { value = property.value.GetRow(1) };
            row1Field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                    Vector4 row0 = property.value.GetRow(0);
                    Vector4 row2 = property.value.GetRow(2);
                    Vector4 row3 = property.value.GetRow(3);
                    property.value = new Matrix4x4()
                    {
                        m00 = row0.x,
                        m01 = row0.y,
                        m02 = row0.z,
                        m03 = row0.w,
                        m10 = evt.newValue.x,
                        m11 = evt.newValue.y,
                        m12 = evt.newValue.z,
                        m13 = evt.newValue.w,
                        m20 = row2.x,
                        m21 = row2.y,
                        m22 = row2.z,
                        m23 = row2.w,
                        m30 = row3.x,
                        m31 = row3.y,
                        m32 = row3.z,
                        m33 = row3.w,
                    };
                    DirtyNodes();
                });
            AddRow("", row1Field);

            var row2Field = new Vector4Field { value = property.value.GetRow(2) };
            row2Field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                    Vector4 row0 = property.value.GetRow(0);
                    Vector4 row1 = property.value.GetRow(1);
                    Vector4 row3 = property.value.GetRow(3);
                    property.value = new Matrix4x4()
                    {
                        m00 = row0.x,
                        m01 = row0.y,
                        m02 = row0.z,
                        m03 = row0.w,
                        m10 = row1.x,
                        m11 = row1.y,
                        m12 = row1.z,
                        m13 = row1.w,
                        m20 = evt.newValue.x,
                        m21 = evt.newValue.y,
                        m22 = evt.newValue.z,
                        m23 = evt.newValue.w,
                        m30 = row3.x,
                        m31 = row3.y,
                        m32 = row3.z,
                        m33 = row3.w,
                    };
                    DirtyNodes();
                });
            AddRow("", row2Field);

            var row3Field = new Vector4Field { value = property.value.GetRow(3) };
            row3Field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                    Vector4 row0 = property.value.GetRow(0);
                    Vector4 row1 = property.value.GetRow(1);
                    Vector4 row2 = property.value.GetRow(2);
                    property.value = new Matrix4x4()
                    {
                        m00 = row0.x,
                        m01 = row0.y,
                        m02 = row0.z,
                        m03 = row0.w,
                        m10 = row1.x,
                        m11 = row1.y,
                        m12 = row1.z,
                        m13 = row1.w,
                        m20 = row2.x,
                        m21 = row2.y,
                        m22 = row2.z,
                        m23 = row2.w,
                        m30 = evt.newValue.x,
                        m31 = evt.newValue.y,
                        m32 = evt.newValue.z,
                        m33 = evt.newValue.w,
                    };
                    DirtyNodes();
                });
            AddRow("", row3Field);
        }

        void BuildSamplerStatePropertyField(SamplerStateShaderProperty property)
        {
            var filterField = new EnumField(property.value.filter);
            filterField.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                    TextureSamplerState state = property.value;
                    state.filter = (TextureSamplerState.FilterMode)evt.newValue;
                    property.value = state;
                    Rebuild();
                    DirtyNodes(ModificationScope.Graph);
                });
            AddRow("Filter", filterField);

            var wrapField = new EnumField(property.value.wrap);
            wrapField.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                    TextureSamplerState state = property.value;
                    state.wrap = (TextureSamplerState.WrapMode)evt.newValue;
                    property.value = state;
                    Rebuild();
                    DirtyNodes(ModificationScope.Graph);
                });
            AddRow("Wrap", wrapField);
        }

        void BuildGradientPropertyField(GradientShaderProperty property)
        {
            var field = new GradientField { value = property.value };
            field.RegisterValueChangedCallback(evt =>
                {
                    graph.owner.RegisterCompleteObjectUndo("Change Property Value");
                    property.value = evt.newValue;
                    DirtyNodes();
                });
            AddRow("Default", field);
        }

        void BuildVirtualTexturePropertyField(VirtualTextureShaderProperty property)
        {
            Toggle proceduralToggle = new Toggle { value = property.value.procedural };
            proceduralToggle.OnToggleChanged(evt =>
            {
                graph.owner.RegisterCompleteObjectUndo("Change VT Procedural Toggle");
                property.value.procedural = evt.newValue;
                DirtyNodes(ModificationScope.Graph);
            });
            AddRow("Procedural", proceduralToggle);
            // TODO: add layer names and texture assignments view here (could we re-use the TextureShaderProperty custom builder?)
            // Func<VisualElement> makeItem = () => new Label();
            // Action<VisualElement, int> bindItem = (e, i) => (e as Label).text = property.value.layerNames.ElementAt(i);
            // const int itemHeight = 16;

            // var listView = new ListView(property.value.layerNames, itemHeight, makeItem, bindItem);
            // listView.style.flexGrow = 1.0f;
            // listView.style.height = property.value.layerNames.Count * itemHeight;
            m_VTContainer = new IMGUIContainer(() => OnGUIHandler(property)) { name = "VTListContainer" };
            AddRow("Entries", m_VTContainer);
        }
        private void OnGUIHandler(VirtualTextureShaderProperty property)
        {
            if(m_VTReorderableList == null)
            {
                RecreateList(property);
                AddCallbacks(property);
            }

            m_VTReorderableList.index = m_VTSelectedIndex;
            m_VTReorderableList.DoLayoutList();
        }

        internal void RecreateList(VirtualTextureShaderProperty property)
        {
            // Create reorderable list from entries
            m_VTReorderableList = new ReorderableList(property.value.entries, typeof(VirtualTextureEntry), true, true, true, true);
        }

        private void AddCallbacks(VirtualTextureShaderProperty property)
        {
            // Draw Header
            m_VTReorderableList.drawHeaderCallback = (Rect rect) =>
            {
                int indent = 14;
                var displayRect = new Rect(rect.x + indent, rect.y, (rect.width - indent) / 2, rect.height);
                EditorGUI.LabelField(displayRect, "Layer Name");
                var referenceRect = new Rect((rect.x + indent) + (rect.width - indent) / 2, rect.y, (rect.width - indent) / 2, rect.height);
                EditorGUI.LabelField(referenceRect, "Texture Asset");
            };

            // Draw Element
            m_VTReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                VirtualTextureEntry entry = ((VirtualTextureEntry)m_VTReorderableList.list[index]);
                EditorGUI.BeginChangeCheck();

                var layerName = EditorGUI.DelayedTextField( new Rect(rect.x, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight), entry.layerName, EditorStyles.label);
                var selectedObject = EditorGUI.ObjectField( new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight), entry.layerTexture.texture, typeof(Texture));

                SerializableTexture layerTexture = new SerializableTexture();
                layerTexture.texture = (Texture)selectedObject;

                if (EditorGUI.EndChangeCheck())
                {
                    property.value.entries[index] = new VirtualTextureEntry(layerName, layerTexture);

                    DirtyNodes();
                    Rebuild();
                }
            };

            // Element height
            m_VTReorderableList.elementHeightCallback = (int indexer) =>
            {
                return m_VTReorderableList.elementHeight;
            };

            // Can add
            m_VTReorderableList.onCanAddCallback = (ReorderableList list) =>
            {
                return list.count < 4;
            };

            // Can remove
            m_VTReorderableList.onCanRemoveCallback = (ReorderableList list) =>
            {
                return list.count > 1;
            };

            void AddEntryLamda(ReorderableList list) => AddEntry(list, property);
            void RemoveEntryLamda(ReorderableList list) => RemoveEntry(list, property);
            // Add callback delegates
            m_VTReorderableList.onSelectCallback += SelectEntry;
            m_VTReorderableList.onAddCallback += AddEntryLamda;
            m_VTReorderableList.onRemoveCallback += RemoveEntryLamda;
            m_VTReorderableList.onReorderCallback += ReorderEntries;
        }

        private void SelectEntry(ReorderableList list)
        {
            m_VTSelectedIndex = list.index;
        }

        private void AddEntry(ReorderableList list, VirtualTextureShaderProperty property)
        {
            graph.owner.RegisterCompleteObjectUndo("Add Virtual Texture Entry");

            int index = GetFirstUnusedID(property);
            if (index <= 0)
                return; // Error has already occured, don't attempt to add this entry.

            var layerName = "Layer" + index.ToString();
            // Add new entry
            property.value.entries.Add(new VirtualTextureEntry(layerName, new SerializableTexture()));

            // Update Blackboard & Nodes
            DirtyNodes();
            Rebuild();
            graph.OnKeywordChanged();
            m_VTSelectedIndex = list.list.Count - 1;
        }

        // Allowed indicies are 1-MAX_ENUM_ENTRIES
        private int GetFirstUnusedID(VirtualTextureShaderProperty property)
        {
            List<int> ususedIDs = new List<int>();

            foreach (VirtualTextureEntry virtualTextureEntry in property.value.entries)
            {
                ususedIDs.Add(property.value.entries.IndexOf(virtualTextureEntry));
            }

            for (int x = 1; x <= 4; x++)
            {
                if (!ususedIDs.Contains(x))
                    return x;
            }

            Debug.LogError("GetFirstUnusedID: Attempting to get unused ID when all IDs are used.");
            return -1;
        }

        private void RemoveEntry(ReorderableList list, VirtualTextureShaderProperty property)
        {
            graph.owner.RegisterCompleteObjectUndo("Remove Virtual Texture Entry");

            // Remove entry
            m_VTSelectedIndex = list.index;
            var selectedEntry = (VirtualTextureEntry)m_VTReorderableList.list[list.index];
            property.value.entries.Remove(selectedEntry);

            // Update Blackboard & Nodes
            DirtyNodes();
            Rebuild();
            graph.OnKeywordChanged();
            m_VTSelectedIndex = m_VTSelectedIndex >= list.list.Count - 1 ? list.list.Count - 1 : m_VTSelectedIndex;
        }

        private void ReorderEntries(ReorderableList list)
        {
            DirtyNodes();
        }


        public override void DirtyNodes(ModificationScope modificationScope = ModificationScope.Node)
        {
            var colorManager = GetFirstAncestorOfType<GraphEditorView>().colorManager;
            var nodes = GetFirstAncestorOfType<GraphEditorView>().graphView.Query<MaterialNodeView>().ToList();

            colorManager.SetNodesDirty(nodes);
            colorManager.UpdateNodeViews(nodes);

            foreach (var node in graph.GetNodes<PropertyNode>())
            {
                node.Dirty(modificationScope);
            }
        }
    }
}
