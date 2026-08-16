using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Samples
{
    /// <summary>
    /// Turns the Effects section's buttons into glowing, shining CTAs — but only when
    /// UIImageEffectsKit is installed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The demo is one sample and its assembly deliberately references nothing but the kit and uGUI,
    /// so it cannot name <c>SDFImage</c> or <c>SDFButtonEffects</c> at compile time. Baking them into
    /// the scene is worse still: without the package they would load as missing scripts and take the
    /// whole scene down with them.
    /// </para>
    /// <para>
    /// So the scene ships with ordinary <see cref="Image"/> buttons and this upgrades them at runtime
    /// if the types are there. <c>SDFImage</c> derives from <see cref="Image"/>, which is what makes
    /// the swap cheap — once it is added, the sprite, colour and type are set through the normal
    /// Image API and only the effects component needs reflection.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class DemoEffects : MonoBehaviour
    {
        /// <summary>Mirrors SDFShineMode in the adapter, which this assembly cannot reference.</summary>
        public enum ShineMode
        {
            Off = 0,
            Loop = 1,
            OnHover = 2,
            OnClick = 3,
        }

        [Serializable]
        private struct Entry
        {
            public EnhancedButton button;
            public ShineMode shine;
            public bool pulse;
        }

        private const string SdfImageType = "SDFImageKit.SDFImage, SDFImageKit.Runtime";
        private const string EffectsType =
            "Kobapps.UIExtensionsKit.Adapters.SDFButtonEffects, Kobapps.UIExtensionsKit.UIImageEffects";

        [SerializeField] private List<Entry> m_Entries = new List<Entry>();
        [SerializeField] private Text m_Status;

        private void Start()
        {
            Type sdfImage = Type.GetType(SdfImageType);
            Type effects = Type.GetType(EffectsType);

            if (sdfImage == null || effects == null)
            {
                Report("UIImageEffectsKit is not installed, so these are plain buttons.\n" +
                       "Install it (Tools ▸ UIExtensionsKit ▸ Settings ▸ Install UIImageEffectsKit) " +
                       "and press Play again — this section upgrades itself.");
                return;
            }

            int upgraded = 0;
            foreach (Entry entry in m_Entries)
                if (entry.button != null && Upgrade(entry, sdfImage, effects))
                    upgraded++;

            Report($"UIImageEffectsKit detected — {upgraded} button(s) upgraded to SDF.\n" +
                   "Glow follows each button's state. Shine sweeps on a loop, on hover, or on click.");
        }

        private bool Upgrade(Entry entry, Type sdfImageType, Type effectsType)
        {
            GameObject host = entry.button.gameObject;

            var original = host.GetComponent<Image>();
            if (original == null) return false;

            // Remember what the plain Image was showing, then hand it over.
            Sprite sprite = original.sprite;
            Color color = original.color;
            Image.Type imageType = original.type;

            // Immediate, because a GameObject may only carry one Graphic and the replacement has to
            // go on in this same frame — a deferred Destroy would collide with the AddComponent.
            DestroyImmediate(original);

            // SDFImage is an Image, so everything except the effects stack is plain uGUI from here.
            var sdf = host.AddComponent(sdfImageType) as Image;
            if (sdf == null) return false;

            sdf.sprite = sprite;
            sdf.color = color;
            sdf.type = imageType;
            entry.button.targetGraphic = sdf;

            // Generate the distance field at runtime; the sample ships no baked SDF assets.
            SetMember(sdf, "generateAtRuntime", true);

            Component fx = host.AddComponent(effectsType);
            SetMember(fx, "m_Target", sdf);
            SetMember(fx, "m_ShineMode", ToEnum(effectsType, "m_ShineMode", (int)entry.shine));
            SetMember(fx, "m_Pulse", entry.pulse);

            // AddComponent ran Awake before those assignments landed, so the component decided which
            // effect layers it needed while the shine mode was still Off. Refresh re-resolves it.
            effectsType.GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(fx, null);

            // The button cached its effect modules when it was enabled, before this one existed.
            entry.button.RefreshEffects();
            return true;
        }

        private static object ToEnum(Type owner, string fieldName, int value)
        {
            FieldInfo field = owner.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? value : Enum.ToObject(field.FieldType, value);
        }

        /// <summary>Set a field or property by name, whether it is public or serialized-private.</summary>
        private static void SetMember(object target, string name, object value)
        {
            if (target == null) return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo field = target.GetType().GetField(name, flags);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = target.GetType().GetProperty(name, flags);
            if (property != null && property.CanWrite) property.SetValue(target, value);
        }

        private void Report(string message)
        {
            if (m_Status != null) m_Status.text = message;
        }
    }
}
