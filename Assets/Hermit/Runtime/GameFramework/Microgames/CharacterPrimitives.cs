using UnityEngine;
using UnityEngine.UI;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// C8.1b: the shared, reusable character-face kit every Gold presenter's
    /// character(s) build from — head/eyes/mouth as plain procedural shapes,
    /// no rigs, no per-character bespoke code. Two eye dots (RectTransform
    /// size = expression) plus one mouth bar (size + color = expression) is
    /// enough to read Idle/Correct/Incorrect at a glance, and is exactly as
    /// cheap to build/reuse as the ASCII-art faces this replaces (still just
    /// a handful of Image property writes, no allocation, no rebuild).
    /// </summary>
    internal static class CharacterPrimitives
    {
        public enum FacePose
        {
            Idle,
            Correct,
            Incorrect
        }

        public struct Face
        {
            public RectTransform LeftEye;
            public RectTransform RightEye;
            public Image LeftEyeImage;
            public Image RightEyeImage;
            public RectTransform Mouth;
            public Image MouthImage;
        }

        /// <summary>Builds two eye dots and one mouth bar as children of
        /// <paramref name="head"/>, sized relative to <paramref name="headSize"/>
        /// so the same call works for a small suspect face or a larger
        /// contestant head. Starts in <see cref="FacePose.Idle"/>.</summary>
        public static Face Build(Transform head, float headSize)
        {
            var eyeColor = new Color(0.16f, 0.13f, 0.11f, 1f);
            var eyeSize = headSize * 0.16f;
            var eyeOffsetX = headSize * 0.19f;
            var eyeOffsetY = headSize * 0.08f;

            var leftEye = RuntimeUIFactory.CreateRoundedPanel(head, "EyeL", eyeColor, Mathf.RoundToInt(eyeSize));
            leftEye.anchorMin = leftEye.anchorMax = new Vector2(0.5f, 0.5f);
            leftEye.anchoredPosition = new Vector2(-eyeOffsetX, eyeOffsetY);
            leftEye.sizeDelta = new Vector2(eyeSize, eyeSize);

            var rightEye = RuntimeUIFactory.CreateRoundedPanel(head, "EyeR", eyeColor, Mathf.RoundToInt(eyeSize));
            rightEye.anchorMin = rightEye.anchorMax = new Vector2(0.5f, 0.5f);
            rightEye.anchoredPosition = new Vector2(eyeOffsetX, eyeOffsetY);
            rightEye.sizeDelta = new Vector2(eyeSize, eyeSize);

            var mouth = RuntimeUIFactory.CreateRoundedPanel(head, "Mouth", eyeColor, Mathf.RoundToInt(headSize * 0.08f));
            mouth.anchorMin = mouth.anchorMax = new Vector2(0.5f, 0.5f);
            mouth.anchoredPosition = new Vector2(0f, -headSize * 0.22f);
            mouth.sizeDelta = new Vector2(headSize * 0.34f, headSize * 0.1f);

            var face = new Face
            {
                LeftEye = leftEye,
                RightEye = rightEye,
                LeftEyeImage = leftEye.GetComponent<Image>(),
                RightEyeImage = rightEye.GetComponent<Image>(),
                Mouth = mouth,
                MouthImage = mouth.GetComponent<Image>(),
            };

            ApplyPose(face, FacePose.Idle, headSize);
            return face;
        }

        /// <summary>Idle: small round eyes, flat mouth. Correct: wide eyes,
        /// wide open mouth, brighter. Incorrect: flat/narrow eyes, short
        /// downturned mouth, muted. Deliberately just size/color changes on
        /// cached references — no rebuild, safe to call every reveal.</summary>
        public static void ApplyPose(Face face, FacePose pose, float headSize)
        {
            switch (pose)
            {
                case FacePose.Correct:
                    face.LeftEye.sizeDelta = face.RightEye.sizeDelta = Vector2.one * (headSize * 0.20f);
                    face.Mouth.sizeDelta = new Vector2(headSize * 0.42f, headSize * 0.16f);
                    face.Mouth.anchoredPosition = new Vector2(0f, -headSize * 0.20f);
                    face.MouthImage.color = new Color(0.55f, 0.16f, 0.14f, 1f);
                    break;

                case FacePose.Incorrect:
                    face.LeftEye.sizeDelta = face.RightEye.sizeDelta = new Vector2(headSize * 0.16f, headSize * 0.05f);
                    face.Mouth.sizeDelta = new Vector2(headSize * 0.22f, headSize * 0.08f);
                    face.Mouth.anchoredPosition = new Vector2(0f, -headSize * 0.26f);
                    face.MouthImage.color = new Color(0.16f, 0.13f, 0.11f, 1f);
                    break;

                default:
                    face.LeftEye.sizeDelta = face.RightEye.sizeDelta = Vector2.one * (headSize * 0.16f);
                    face.Mouth.sizeDelta = new Vector2(headSize * 0.34f, headSize * 0.1f);
                    face.Mouth.anchoredPosition = new Vector2(0f, -headSize * 0.22f);
                    face.MouthImage.color = new Color(0.16f, 0.13f, 0.11f, 1f);
                    break;
            }
        }
    }
}
