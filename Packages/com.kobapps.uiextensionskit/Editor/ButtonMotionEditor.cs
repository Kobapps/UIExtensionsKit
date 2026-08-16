using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// A flat, one-state-at-a-time editor for a <see cref="ButtonMotionSet"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default inspector for a motion set is five nested foldouts of eight fields each, plus the
    /// punch — around fifty raw float boxes, with the one you want always collapsed. Tuning a feel
    /// that way is miserable, and a feel you cannot tune quickly is a feel nobody tunes.
    /// </para>
    /// <para>
    /// So: pick a state, see only that state, and move sliders with ranges chosen to make the useful
    /// values easy to hit and the silly ones hard. Every edit goes through
    /// <see cref="SerializedObject"/>, so Undo and multi-object editing behave normally, and every
    /// edit reports back so the live preview updates on the same frame rather than on a timer.
    /// </para>
    /// </remarks>
    public sealed class ButtonMotionEditor : VisualElement
    {
        private static readonly EnhancedButtonVisualState[] States =
            (EnhancedButtonVisualState[])Enum.GetValues(typeof(EnhancedButtonVisualState));

        private readonly SerializedObject _serialized;
        private readonly string _root;
        private readonly Action _onChanged;
        private readonly VisualElement _stateBody;
        private readonly EaseThumbnail _thumbnail;

        private EnhancedButtonVisualState _state = EnhancedButtonVisualState.Normal;
        private bool _allStates;
        private readonly RadioButtonGroup _stateTabs;

        /// <summary>Raised when the edited state changes, so a host can mirror it in a preview.</summary>
        public event Action<EnhancedButtonVisualState> StateFocused;

        public ButtonMotionEditor(SerializedObject serialized, string rootPath, Action onChanged)
        {
            _serialized = serialized;
            _root = rootPath;
            _onChanged = onChanged;

            var names = new string[States.Length];
            for (int i = 0; i < States.Length; i++) names[i] = States[i].ToString();

            var viewToggle = new Toggle("Show all states") { value = false };
            viewToggle.RegisterValueChangedCallback(e =>
            {
                _allStates = e.newValue;
                RebuildAll();
            });
            Add(viewToggle);

            _stateTabs = new RadioButtonGroup("State", new List<string>(names)) { value = 0 };
            _stateTabs.RegisterValueChangedCallback(e =>
            {
                if (e.newValue < 0 || e.newValue >= States.Length) return;

                _state = States[e.newValue];
                RebuildStateBody();
                StateFocused?.Invoke(_state);
            });
            Add(_stateTabs);

            _thumbnail = new EaseThumbnail();
            _stateBody = new VisualElement();
            Add(_stateBody);

            Add(BuildPunch());
            Add(BuildShine());
            RebuildAll();
        }

        /// <summary>Path to a field of the state currently being edited.</summary>
        private string Path(string field) => $"{_root}.{StateField(_state)}.{field}";

        private static string StateField(EnhancedButtonVisualState state)
        {
            switch (state)
            {
                case EnhancedButtonVisualState.Highlighted: return "highlighted";
                case EnhancedButtonVisualState.Pressed: return "pressed";
                case EnhancedButtonVisualState.Selected: return "selected";
                case EnhancedButtonVisualState.Disabled: return "disabled";
                case EnhancedButtonVisualState.Cta: return "cta";
                default: return "normal";
            }
        }

        #region State body

        private void RebuildStateBody()
        {
            _stateBody.Clear();

            _stateBody.Add(ScaleRow());
            _stateBody.Add(FloatSlider("Offset X", Path("offset"), -40f, 40f, axis: 0));
            _stateBody.Add(FloatSlider("Offset Y", Path("offset"), -40f, 40f, axis: 1));
            _stateBody.Add(FloatSlider("Rotation", Path("rotation"), -30f, 30f));
            _stateBody.Add(ColorRow("Tint", Path("tint")));
            _stateBody.Add(ColorRow("Label Tint", Path("labelTint")));
            _stateBody.Add(FloatSlider("Duration", Path("duration"), 0f, 1f));
            _stateBody.Add(EaseRow());

            _stateBody.Add(InspectorUI.Row(
                Ghost("Reset state", ResetState),
                Ghost("Copy from Normal", () => CopyFrom(EnhancedButtonVisualState.Normal)),
                Ghost("Copy from Highlighted", () => CopyFrom(EnhancedButtonVisualState.Highlighted))));

            RefreshThumbnail();
        }


        private void RebuildAll()
        {
            _stateTabs.style.display = _allStates ? DisplayStyle.None : DisplayStyle.Flex;

            if (_allStates) RebuildAllStatesBody();
            else RebuildStateBody();
        }

        /// <summary>
        /// Every state at once, one row each.
        /// </summary>
        /// <remarks>
        /// The focused view is better for tuning a single state; this is better for the questions
        /// that only make sense across states - is Pressed faster than Highlighted, do the durations
        /// form a sensible rhythm, is one state still on a default ease nobody chose.
        /// </remarks>
        private void RebuildAllStatesBody()
        {
            _stateBody.Clear();

            _stateBody.Add(InspectorUI.Muted(
                "A transition belongs to the state being entered: leaving Highlighted for Normal uses " +
                "Normal's duration and ease. Set a state's timing to set how buttons arrive at it."));

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            header.Add(HeaderCell("State", 92f));
            header.Add(HeaderCell("Scale", 0f));
            header.Add(HeaderCell("Duration", 0f));
            header.Add(HeaderCell("Ease", 0f));
            header.Add(HeaderCell("Tint", 0f));
            _stateBody.Add(header);

            foreach (EnhancedButtonVisualState state in States)
                _stateBody.Add(StateRow(state));

            _stateBody.Add(InspectorUI.Row(
                Ghost("Even out durations", EvenDurations),
                Ghost("Reset every state", ResetAllStates)));
        }

        private static Label HeaderCell(string text, float width)
        {
            var label = new Label(text) { style = { opacity = 0.6f, fontSize = 11 } };
            if (width > 0f) label.style.width = width;
            else label.style.flexGrow = 1;
            return label;
        }

        private VisualElement StateRow(EnhancedButtonVisualState state)
        {
            string prefix = _root + "." + StateField(state);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

            row.Add(new Label(state.ToString())
            {
                style = { width = 92f, unityTextAlign = TextAnchor.MiddleLeft },
            });

            SerializedProperty scaleProperty = _serialized.FindProperty(prefix + ".scale");
            var scale = new FloatField { value = scaleProperty.vector3Value.x, style = { flexGrow = 1 } };
            scale.RegisterValueChangedCallback(e =>
                WriteVector3(prefix + ".scale", new Vector3(e.newValue, e.newValue, 1f)));
            row.Add(scale);

            SerializedProperty durationProperty = _serialized.FindProperty(prefix + ".duration");
            var duration = new FloatField { value = durationProperty.floatValue, style = { flexGrow = 1 } };
            duration.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                _serialized.FindProperty(prefix + ".duration").floatValue = Mathf.Max(0f, e.newValue);
                Commit();
            });
            row.Add(duration);

            SerializedProperty easeProperty = _serialized.FindProperty(prefix + ".ease");
            var ease = new EnumField((UIEase)easeProperty.enumValueIndex) { style = { flexGrow = 1 } };
            ease.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                _serialized.FindProperty(prefix + ".ease").enumValueIndex = Convert.ToInt32(e.newValue);
                Commit();
            });
            row.Add(ease);

            SerializedProperty tintProperty = _serialized.FindProperty(prefix + ".tint");
            var tint = new ColorField { value = tintProperty.colorValue, style = { flexGrow = 1 } };
            tint.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                _serialized.FindProperty(prefix + ".tint").colorValue = e.newValue;
                Commit();
            });
            row.Add(tint);

            // Jump straight from a row into the focused editor for that state.
            Button focus = InspectorUI.Action("Edit", () =>
            {
                _allStates = false;
                _state = state;
                RebuildAll();
                StateFocused?.Invoke(state);
            });
            focus.style.width = 52f;
            row.Add(focus);

            return row;
        }

        /// <summary>Give every state the same timing - the fastest way to calm an inconsistent preset.</summary>
        private void EvenDurations()
        {
            _serialized.Update();

            float total = 0f;
            foreach (EnhancedButtonVisualState state in States)
                total += _serialized.FindProperty(_root + "." + StateField(state) + ".duration").floatValue;

            float average = Mathf.Max(0.02f, total / States.Length);
            foreach (EnhancedButtonVisualState state in States)
                _serialized.FindProperty(_root + "." + StateField(state) + ".duration").floatValue = average;

            Commit();
            RebuildAll();
        }

        private void ResetAllStates()
        {
            EnhancedButtonVisualState previous = _state;

            foreach (EnhancedButtonVisualState state in States)
            {
                _state = state;
                ResetState();
            }

            _state = previous;
            RebuildAll();
        }

        /// <summary>
        /// Scale gets a uniform slider plus per-axis fields, because uniform is what people want
        /// nine times in ten and squash-and-stretch is the tenth.
        /// </summary>
        private VisualElement ScaleRow()
        {
            SerializedProperty property = _serialized.FindProperty(Path("scale"));
            Vector3 value = property != null ? property.vector3Value : Vector3.one;

            var uniform = new Slider("Scale", 0.5f, 1.5f) { value = value.x, showInputField = true };
            var x = new FloatField("  X") { value = value.x };
            var y = new FloatField("  Y") { value = value.y };

            uniform.RegisterValueChangedCallback(e =>
            {
                x.SetValueWithoutNotify(e.newValue);
                y.SetValueWithoutNotify(e.newValue);
                WriteVector3(Path("scale"), new Vector3(e.newValue, e.newValue, 1f));
            });

            x.RegisterValueChangedCallback(e =>
            {
                uniform.SetValueWithoutNotify(e.newValue);
                WriteVector3(Path("scale"), new Vector3(e.newValue, y.value, 1f));
            });

            y.RegisterValueChangedCallback(e =>
                WriteVector3(Path("scale"), new Vector3(x.value, e.newValue, 1f)));

            var row = new VisualElement();
            row.Add(uniform);

            var axes = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            x.style.flexGrow = 1;
            y.style.flexGrow = 1;
            axes.Add(x);
            axes.Add(y);
            row.Add(axes);

            return row;
        }

        private VisualElement FloatSlider(string label, string path, float min, float max, int axis = -1)
        {
            SerializedProperty property = _serialized.FindProperty(path);
            if (property == null) return new Label($"missing: {path}");

            float current = axis < 0
                ? property.floatValue
                : (axis == 0 ? property.vector2Value.x : property.vector2Value.y);

            var slider = new Slider(label, min, max) { value = current, showInputField = true };

            slider.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                SerializedProperty live = _serialized.FindProperty(path);

                if (axis < 0)
                {
                    live.floatValue = e.newValue;
                }
                else
                {
                    Vector2 vector = live.vector2Value;
                    if (axis == 0) vector.x = e.newValue;
                    else vector.y = e.newValue;
                    live.vector2Value = vector;
                }

                Commit();
            });

            return slider;
        }

        private VisualElement ColorRow(string label, string path)
        {
            SerializedProperty property = _serialized.FindProperty(path);
            if (property == null) return new Label($"missing: {path}");

            var field = new ColorField(label) { value = property.colorValue };
            field.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                _serialized.FindProperty(path).colorValue = e.newValue;
                Commit();
            });

            return field;
        }

        private VisualElement EaseRow()
        {
            SerializedProperty easeProperty = _serialized.FindProperty(Path("ease"));
            var ease = (UIEase)easeProperty.enumValueIndex;

            var field = new EnumField("Ease", ease);
            field.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                _serialized.FindProperty(Path("ease")).enumValueIndex = Convert.ToInt32(e.newValue);
                Commit();
                RebuildStateBody();
            });

            var row = new VisualElement();
            row.Add(field);
            row.Add(_thumbnail);

            // The curve field only means anything for Custom, and showing it otherwise invites
            // someone to draw a curve that is then silently ignored.
            if (ease == UIEase.Custom)
            {
                SerializedProperty curveProperty = _serialized.FindProperty(Path("curve"));
                var curve = new CurveField("  Curve") { value = curveProperty.animationCurveValue };
                curve.RegisterValueChangedCallback(e =>
                {
                    _serialized.Update();
                    _serialized.FindProperty(Path("curve")).animationCurveValue = e.newValue;
                    Commit();
                    RefreshThumbnail();
                });

                row.Add(curve);
                row.Add(Ghost("Reset curve to ease-out", () =>
                {
                    _serialized.Update();
                    _serialized.FindProperty(Path("curve")).animationCurveValue =
                        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                    Commit();
                    RebuildStateBody();
                }));
            }

            return row;
        }

        #endregion

        #region Punch

        private VisualElement BuildPunch()
        {
            var section = InspectorUI.Section("Click punch", true, "Kobapps.UIExtensionsKit.PunchSection");

            string punch = $"{_root}.click";
            SerializedProperty enabled = _serialized.FindProperty($"{punch}.enabled");

            var toggle = new Toggle("Enabled") { value = enabled != null && enabled.boolValue };
            toggle.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                _serialized.FindProperty($"{punch}.enabled").boolValue = e.newValue;
                Commit();
            });

            section.Add(toggle);
            section.Add(PunchAxis("Amplitude X", $"{punch}.scaleAmplitude", 0));
            section.Add(PunchAxis("Amplitude Y", $"{punch}.scaleAmplitude", 1));
            section.Add(FloatSliderAt("Rotation", $"{punch}.rotationAmplitude", -30f, 30f));
            section.Add(FloatSliderAt("Duration", $"{punch}.duration", 0f, 1.5f));
            section.Add(IntSliderAt("Oscillations", $"{punch}.oscillations", 1, 8));
            section.Add(FloatSliderAt("Damping", $"{punch}.damping", 0.1f, 6f));

            return section;
        }

        #endregion

        #region Shine

        private VisualElement BuildShine()
        {
            var section = InspectorUI.Section("Shine (CTA)", false, "Kobapps.UIExtensionsKit.ShineSection");

            section.Add(InspectorUI.Muted(
                "A band of light sweeping across the button. Drawn by an effects module — the CTA " +
                "trigger runs it whenever the button is marked as the call to action."));

            string shine = $"{_root}.shine";
            var trigger = new PropertyField(_serialized.FindProperty($"{shine}.trigger"), "Trigger");
            trigger.Bind(_serialized);
            section.Add(trigger);

            var body = new VisualElement();
            section.Add(body);

            body.Add(FloatSliderAt("Sweep duration", $"{shine}.sweepDuration", 0.1f, 3f));
            body.Add(FloatSliderAt("Interval", $"{shine}.interval", 0f, 10f));
            body.Add(FloatSliderAt("Width", $"{shine}.width", 0.02f, 1f));
            body.Add(FloatSliderAt("Softness", $"{shine}.softness", 0f, 1f));
            body.Add(FloatSliderAt("Angle", $"{shine}.angle", 0f, 360f));
            body.Add(ColorAt("Colour", $"{shine}.color"));

            // Interval only means anything for the repeating triggers; hiding it beats explaining it.
            var interval = body[1];
            void Sync()
            {
                SerializedProperty prop = _serialized.FindProperty($"{shine}.trigger");
                if (prop == null) return;

                var mode = (ButtonShineTrigger)prop.enumValueIndex;
                body.style.display = mode == ButtonShineTrigger.Off ? DisplayStyle.None : DisplayStyle.Flex;
                interval.style.display =
                    mode == ButtonShineTrigger.Cta || mode == ButtonShineTrigger.Always
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            Sync();
            trigger.RegisterValueChangeCallback(_ => Sync());

            return section;
        }

        private VisualElement ColorAt(string label, string path)
        {
            SerializedProperty prop = _serialized.FindProperty(path);
            var field = new ColorField(label) { value = prop != null ? prop.colorValue : Color.white };
            field.RegisterValueChangedCallback(e =>
            {
                SerializedProperty live = _serialized.FindProperty(path);
                if (live == null) return;

                live.colorValue = e.newValue;
                Commit();
            });

            return field;
        }

        #endregion

        #region Punch helpers

        private VisualElement PunchAxis(string label, string path, int axis)
        {
            SerializedProperty property = _serialized.FindProperty(path);
            Vector3 value = property != null ? property.vector3Value : Vector3.zero;

            var slider = new Slider(label, -0.5f, 0.5f)
            {
                value = axis == 0 ? value.x : value.y,
                showInputField = true,
            };

            slider.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                SerializedProperty live = _serialized.FindProperty(path);
                Vector3 vector = live.vector3Value;
                if (axis == 0) vector.x = e.newValue;
                else vector.y = e.newValue;
                live.vector3Value = vector;
                Commit();
            });

            return slider;
        }

        private VisualElement FloatSliderAt(string label, string path, float min, float max)
        {
            SerializedProperty property = _serialized.FindProperty(path);
            if (property == null) return new Label($"missing: {path}");

            var slider = new Slider(label, min, max) { value = property.floatValue, showInputField = true };
            slider.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                _serialized.FindProperty(path).floatValue = e.newValue;
                Commit();
            });

            return slider;
        }

        private VisualElement IntSliderAt(string label, string path, int min, int max)
        {
            SerializedProperty property = _serialized.FindProperty(path);
            if (property == null) return new Label($"missing: {path}");

            var slider = new SliderInt(label, min, max) { value = property.intValue, showInputField = true };
            slider.RegisterValueChangedCallback(e =>
            {
                _serialized.Update();
                _serialized.FindProperty(path).intValue = e.newValue;
                Commit();
            });

            return slider;
        }

        #endregion

        #region Plumbing

        private void WriteVector3(string path, Vector3 value)
        {
            _serialized.Update();
            _serialized.FindProperty(path).vector3Value = value;
            Commit();
        }

        private void Commit()
        {
            // The edited asset may have been deleted out from under this element.
            if (_serialized == null || _serialized.targetObject == null) return;

            _serialized.ApplyModifiedProperties();
            RefreshThumbnail();
            _onChanged?.Invoke();
        }

        private void ResetState()
        {
            _serialized.Update();

            ButtonStateMotion identity = ButtonStateMotion.Identity;
            SerializedProperty state = _serialized.FindProperty($"{_root}.{StateField(_state)}");

            state.FindPropertyRelative("scale").vector3Value = identity.scale;
            state.FindPropertyRelative("offset").vector2Value = identity.offset;
            state.FindPropertyRelative("rotation").floatValue = identity.rotation;
            state.FindPropertyRelative("tint").colorValue = identity.tint;
            state.FindPropertyRelative("labelTint").colorValue = identity.labelTint;
            state.FindPropertyRelative("duration").floatValue = identity.duration;
            state.FindPropertyRelative("ease").enumValueIndex = (int)identity.ease;

            Commit();
            RebuildAll();
        }

        private void CopyFrom(EnhancedButtonVisualState source)
        {
            if (source == _state) return;

            _serialized.Update();

            SerializedProperty from = _serialized.FindProperty($"{_root}.{StateField(source)}");
            SerializedProperty to = _serialized.FindProperty($"{_root}.{StateField(_state)}");

            foreach (string field in new[] { "scale", "offset", "rotation", "tint", "labelTint", "duration", "ease" })
            {
                SerializedProperty a = from.FindPropertyRelative(field);
                SerializedProperty b = to.FindPropertyRelative(field);

                switch (a.propertyType)
                {
                    case SerializedPropertyType.Vector3: b.vector3Value = a.vector3Value; break;
                    case SerializedPropertyType.Vector2: b.vector2Value = a.vector2Value; break;
                    case SerializedPropertyType.Float: b.floatValue = a.floatValue; break;
                    case SerializedPropertyType.Color: b.colorValue = a.colorValue; break;
                    case SerializedPropertyType.Enum: b.enumValueIndex = a.enumValueIndex; break;
                }
            }

            Commit();
            RebuildStateBody();
        }

        private void RefreshThumbnail()
        {
            SerializedProperty easeProperty = _serialized.FindProperty(Path("ease"));
            if (easeProperty == null) return;

            var ease = (UIEase)easeProperty.enumValueIndex;
            AnimationCurve curve = null;

            if (ease == UIEase.Custom)
            {
                SerializedProperty curveProperty = _serialized.FindProperty(Path("curve"));
                curve = curveProperty != null ? curveProperty.animationCurveValue : null;
            }

            _thumbnail.Show(ease, curve);
        }

        private static Button Ghost(string text, Action onClick) => InspectorUI.Action(text, onClick);

        #endregion

        /// <summary>
        /// Draws the selected easing curve. A named ease is a word until you see its shape — this is
        /// the difference between picking OutBack because it sounds right and picking it because you
        /// can see it overshoot.
        /// </summary>
        private sealed class EaseThumbnail : VisualElement
        {
            private UIEase _ease = UIEase.OutQuad;
            private AnimationCurve _curve;

            public EaseThumbnail()
            {
                style.height = 64;
                style.marginTop = 4;
                style.marginBottom = 6;
                generateVisualContent += Draw;
            }

            public void Show(UIEase ease, AnimationCurve curve)
            {
                _ease = ease;
                _curve = curve;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f) return;

                Painter2D painter = context.painter2D;

                // Overshooting curves leave the 0..1 band, so the plot keeps headroom above and below.
                const float head = 0.25f;
                float Plot(float value) => rect.height * (1f - (value + head) / (1f + head * 2f));

                painter.strokeColor = new Color(1f, 1f, 1f, 0.12f);
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, Plot(0f)));
                painter.LineTo(new Vector2(rect.width, Plot(0f)));
                painter.MoveTo(new Vector2(0f, Plot(1f)));
                painter.LineTo(new Vector2(rect.width, Plot(1f)));
                painter.Stroke();

                painter.strokeColor = new Color(0.45f, 0.75f, 1f, 1f);
                painter.lineWidth = 2f;
                painter.BeginPath();

                const int steps = 64;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float value = _ease == UIEase.Custom
                        ? (_curve != null && _curve.length > 0 ? _curve.Evaluate(t) : t)
                        : UIEasing.Evaluate(_ease, t);

                    var point = new Vector2(rect.width * t, Plot(value));
                    if (i == 0) painter.MoveTo(point);
                    else painter.LineTo(point);
                }

                painter.Stroke();
            }
        }
    }
}
