using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Kobapps.UIExtensionsKit.Samples
{
    /// <summary>
    /// Guarantees the scene's <see cref="EventSystem"/> has an input module, whichever input backend
    /// the project is set to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A shipped scene has to pick one module at author time, and either choice is wrong somewhere:
    /// <c>StandaloneInputModule</c> is inert on a project set to the new Input System, and
    /// <c>InputSystemUIInputModule</c> is a missing script on a project that doesn't have the package.
    /// Either way every button in the sample silently stops responding — the failure looks like the
    /// buttons are broken rather than like the scene is misconfigured.
    /// </para>
    /// <para>
    /// So the scene ships with no module and this adds the right one at runtime. The Input System
    /// type is resolved by name rather than referenced, which keeps the sample assembly free of any
    /// dependency on <c>com.unity.inputsystem</c> — the kit itself does not require it.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    [RequireComponent(typeof(EventSystem))]
    [DefaultExecutionOrder(-100)]
    public sealed class DemoInputBootstrap : MonoBehaviour
    {
        private const string InputSystemModule =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        private void Awake()
        {
            // Respect a module someone added by hand.
            if (GetComponent<BaseInputModule>() != null) return;

            Type moduleType = Type.GetType(InputSystemModule);

            if (moduleType != null)
            {
                gameObject.AddComponent(moduleType);
                return;
            }

            // No Input System package, so the project must be on the legacy input manager.
            gameObject.AddComponent<StandaloneInputModule>();
        }
    }
}
