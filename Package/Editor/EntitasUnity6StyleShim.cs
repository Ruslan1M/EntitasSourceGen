#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EntitasSourceGen.Editor
{
    /// <summary>
    /// Entitas 1.14 Visual Debugging (built for Unity 2021) asks for the misspelled styles
    /// "ToolbarSeach*". Unity 6 only ships "ToolbarSearch*", so the entity inspector throws NRE.
    /// This adds the old names as aliases to the built-in editor skins.
    /// </summary>
    [InitializeOnLoad]
    static class EntitasUnity6StyleShim
    {
        static readonly (string legacy, string current)[] Aliases =
        {
            ("ToolbarSeachTextField", "ToolbarSearchTextField"),
            ("ToolbarSeachCancelButton", "ToolbarSearchCancelButton"),
            ("ToolbarSeachCancelButtonEmpty", "ToolbarSearchCancelButtonEmpty"),
        };

        static EntitasUnity6StyleShim()
        {
            Patch();
            EditorApplication.delayCall += Patch;
        }

        static void Patch()
        {
            foreach (var skinType in new[] { EditorSkin.Inspector, EditorSkin.Game, EditorSkin.Scene })
            {
                var skin = EditorGUIUtility.GetBuiltinSkin(skinType);
                if (skin == null) continue;

                var styles = skin.customStyles.ToList();
                var changed = false;
                foreach (var (legacy, current) in Aliases)
                {
                    if (skin.FindStyle(legacy) != null) continue;
                    var source = skin.FindStyle(current);
                    if (source == null) continue;
                    styles.Add(new GUIStyle(source) { name = legacy });
                    changed = true;
                }

                if (changed) skin.customStyles = styles.ToArray();
            }
        }
    }
}
#endif
