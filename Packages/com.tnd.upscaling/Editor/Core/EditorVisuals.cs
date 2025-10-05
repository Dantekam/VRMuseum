using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static System.Net.WebRequestMethods;

namespace TND.Upscaling.Framework
{
    public class EditorVisuals : MonoBehaviour
    {
        public static string documentation = "https://docs.google.com/document/d/1X4ayGIDx-7bRk4p_B4vmJNvS9W0UDUuAmkxtlerajwY";
        public static string reviewUrl = "https://assetstore.unity.com/publishers/14941";

        private static Texture s_logo;
        private static Texture2D s_background;
        private static Texture2D s_questionMark;
        private static Texture2D s_stars;

        public static Texture2D QuestionMark
        {
            get
            {
                if (!s_questionMark)
                {
                    s_questionMark = Resources.Load<Texture2D>("tnd_question") as Texture2D;
                }
                return s_questionMark;
            }
        }
        public static Texture2D Stars
        {
            get
            {
                if (!s_stars)
                {
                    s_stars = Resources.Load<Texture2D>("tnd_stars") as Texture2D;
                }
                return s_stars;
            }
        }

        private static readonly float HeaderSize = 80;
        private static readonly float FooterSize = 60;

        /// <summary>
        /// Draws the Header area
        /// </summary>
        public static void GenerateHeader()
        {
            if (!s_background)
            {
                s_background = GenerateRadialGradient(256);
            }

            if (!s_logo)
            {
                s_logo = Resources.Load<Texture2D>("tnd_logo") as Texture2D;
            }

            //Draw BG
            var rect = GUILayoutUtility.GetRect(0, 10000, HeaderSize, HeaderSize);
            GUI.DrawTexture(rect, s_background, ScaleMode.StretchToFill);

            //Draw Logo
            float x = rect.x + (rect.width - HeaderSize) / 2;
            float y = rect.y + (rect.height - HeaderSize) / 2;
            GUI.DrawTexture(new Rect(x, y, HeaderSize, HeaderSize), s_logo, ScaleMode.ScaleToFit, true);

            //Clickable Header
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (GUI.Button(rect, "", new GUIStyle(GUI.skin.label)))
            {
                Application.OpenURL("https://prf.hn/l/8jmxEOJ");
            }

            //Clickable Documentation Button
            var docRect = GUILayoutUtility.GetRect(0, 10000, 16, 16);
            EditorGUIUtility.AddCursorRect(docRect, MouseCursor.Link);
            GUIStyle buttonStyleDocumentation = new GUIStyle(GUI.skin.textArea)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.3f, 0.3f, 1) },
                hover = { textColor = Color.white },
                richText = true,
                border = new RectOffset(0, 0, 10, 10)
            };
            if (GUI.Button(docRect, "Online Documentation", buttonStyleDocumentation))
            {
                Application.OpenURL(documentation);
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws the footer area
        /// </summary>
        public static void GenerateFooter()
        {
            if (!s_background)
            {
                s_background = GenerateRadialGradient(256);
            }

            EditorGUILayout.Space();

            // Tekst gecentreerd
            GUIStyle centerLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                normal = { textColor = Color.white }
            };

            GUILayout.Label("Please consider leaving a review!", centerLabel);

            // Klikbare icon buttons
            // Stijl voor icoon-knoppen zonder achtergrond/padding
            GUIStyle iconButton = new GUIStyle(GUI.skin.label)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin  = new RectOffset(0, 0, 0, 0),
                fixedWidth = 30,
                fixedHeight = 30,
            };

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int i = 0; i < 5; i++)
            {
                // Teken de knop met alleen de texture
                if (GUILayout.Button(new GUIContent(Stars), iconButton, GUILayout.Width(30), GUILayout.Height(30)))
                {
                    Application.OpenURL(reviewUrl);
                }
                // Link-cursor over de laatst getekende knop
                var last = GUILayoutUtility.GetLastRect();
                EditorGUIUtility.AddCursorRect(last, MouseCursor.Link);

                if (i < 4) GUILayout.Space(4);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();


            EditorGUILayout.Space();

            //Draw BG
            var rect = GUILayoutUtility.GetRect(0, 10000, FooterSize, FooterSize);
            GUI.DrawTexture(rect, s_background, ScaleMode.StretchToFill);

            //Clickable Footer
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { background = s_background, textColor = new Color(0.9215686274509804f, 0.5372549019607843f, 0.4431372549019608f) },
                hover = { background = s_background, textColor = Color.white },
                richText = true,
            };

            if (GUI.Button(rect, "Click here for more \n 'The Naked Dev' assets!", buttonStyle))
            {
                Application.OpenURL("https://prf.hn/l/8jmxEOJ");
            }
        }

        /// <summary>
        ///  Generates the BG texture for the header
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private static Texture2D GenerateRadialGradient(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            var center = new Vector2(size / 2f, size / 2f);
            float maxDist = center.magnitude;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = dist / maxDist;
                    float brightness = Mathf.Lerp(0.05f, 0.2f, 1 - t);
                    tex.SetPixel(x, y, new Color(brightness, brightness, brightness, 1f));
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
