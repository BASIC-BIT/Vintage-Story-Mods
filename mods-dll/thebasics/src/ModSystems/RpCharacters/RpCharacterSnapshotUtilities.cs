using Vintagestory.API.Datastructures;

namespace thebasics.ModSystems.RpCharacters;

internal static class RpCharacterSnapshotUtilities
{
    public static byte[] ToBytes(ITreeAttribute tree)
    {
        return tree is TreeAttribute treeAttribute ? treeAttribute.ToBytes() : null;
    }

    public static TreeAttribute FromBytes(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return null;
        }

        var tree = new TreeAttribute();
        tree.FromBytes(data);
        return tree;
    }
}
