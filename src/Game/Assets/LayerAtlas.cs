using Godot;

namespace WorldEdit.Assets;

public static class LayerAtlas
{
    public static Texture2D Build(Godot.Collections.Array<Image> images, out int layers)
    {
        layers = images.Count;
        if (layers == 0)
        {
            return null;
        }

        Image first = images[0];
        int width = first.GetWidth();
        int height = first.GetHeight();

        Image atlas = Image.CreateEmpty(width, height * layers, false, Image.Format.Rgba8);
        for (int i = 0; i < layers; i++)
        {
            Image layer = images[i];
            if (layer.GetWidth() != width || layer.GetHeight() != height)
            {
                layer = Resize(layer, width, height);
            }
            atlas.BlitRect(layer, new Rect2I(0, 0, width, height), new Vector2I(0, i * height));
        }

        atlas.GenerateMipmaps();
        return ImageTexture.CreateFromImage(atlas);
    }

    private static Image Resize(Image image, int width, int height)
    {
        var copy = (Image)image.Duplicate();
        copy.Resize(width, height);
        return copy;
    }
}
