namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Union-find over item indices; makes duplicate groups transitive (DF-R3).
/// </summary>
internal sealed class DisjointSet
{
    private readonly int[] _parent;

    public DisjointSet(int count)
    {
        _parent = new int[count];
        for (var i = 0; i < count; i++)
        {
            _parent[i] = i;
        }
    }

    public int Find(int index)
    {
        while (_parent[index] != index)
        {
            _parent[index] = _parent[_parent[index]];
            index = _parent[index];
        }

        return index;
    }

    public void Union(int a, int b)
    {
        var rootA = Find(a);
        var rootB = Find(b);
        if (rootA != rootB)
        {
            _parent[rootB] = rootA;
        }
    }
}
