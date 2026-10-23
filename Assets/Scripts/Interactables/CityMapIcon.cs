using System;
using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    // Display the owner's selected generated artwork; no procedural icon geometry.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CityMapIcon : Image
    {
        private Sprite ownedSprite;
        public CityTradeKind Role { get; private set; }

        public void Configure(CityTradeKind role)
        {
            Role = role;
            var atlas = Resources.Load<Texture2D>("CityMapIcons");
            if (atlas == null) throw new InvalidOperationException("CityMapIcons artwork is missing from Resources.");
            // Source rectangles preserve the selected image, lighting and alpha.
            var source = role == CityTradeKind.Seller ? new Rect(142, 533, 509, 413) :
                role == CityTradeKind.Workshop ? new Rect(100, 87, 605, 381) : new Rect(828, 535, 569, 329);
            ownedSprite = Sprite.Create(atlas, source, Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            ownedSprite.name = "City map " + role;
            sprite = ownedSprite;
            preserveAspect = true;
        }

        protected override void OnDestroy()
        {
            if (ownedSprite != null) Destroy(ownedSprite);
            base.OnDestroy();
        }
    }
}
