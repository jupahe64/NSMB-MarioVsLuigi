using UnityEngine;

namespace NsmbDs {
    /// <summary>Base project art for items and the large Mario setup, gathered by NSMB DS > Build Mario Prefab.</summary>
    public sealed class NsmbItemAssets : ScriptableObject {
        public Sprite mushroom;
        public Sprite fireFlower;
        public Sprite star;
        public Sprite mega;
        public Sprite mini;
        public Sprite shell;
        public Sprite oneUp;
        public Sprite[] goomba;
        public Avatar smallAvatar;
        public Avatar largeAvatar;
        public RuntimeAnimatorController smallController;
        public RuntimeAnimatorController largeController;

        public Sprite SpriteOf(NsmbItemKind item) {
            return item switch {
                NsmbItemKind.FireFlower => fireFlower,
                NsmbItemKind.Star => star,
                NsmbItemKind.Mega => mega,
                NsmbItemKind.Mini => mini,
                NsmbItemKind.Shell => shell,
                NsmbItemKind.OneUp => oneUp,
                _ => mushroom,
            };
        }
    }
}
