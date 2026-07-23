namespace VisionNet.DataType
{
    public struct CxRun
    {
        public int Row;
        public int ColStart;
        public int ColEnd;

        public CxRun(int row, int colStart, int colEnd)
        {
            Row = row;
            ColStart = colStart;
            ColEnd = colEnd;
        }

        public int Length => ColEnd - ColStart;
    }

    public struct CxRegion2D
    {
        public int Width;
        public int Height;
        public CxRun[] Runs;

        public CxRegion2D(int width, int height, CxRun[] runs)
        {
            Width = width;
            Height = height;
            Runs = runs ?? System.Array.Empty<CxRun>();
        }

        public bool IsEmpty => Runs == null || Runs.Length == 0;

        public int Area
        {
            get
            {
                int sum = 0;
                if (Runs != null)
                    for (int i = 0; i < Runs.Length; i++)
                        sum += Runs[i].ColEnd - Runs[i].ColStart;
                return sum;
            }
        }

        public void BoundingBox(out int top, out int left, out int bottom, out int right)
        {
            top = bottom = left = right = 0;
            if (Runs == null || Runs.Length == 0) return;
            top = Runs[0].Row;
            bottom = Runs[0].Row;
            left = Runs[0].ColStart;
            right = Runs[0].ColEnd;
            for (int i = 1; i < Runs.Length; i++)
            {
                ref var r = ref Runs[i];
                if (r.Row < top) top = r.Row;
                if (r.Row > bottom) bottom = r.Row;
                if (r.ColStart < left) left = r.ColStart;
                if (r.ColEnd > right) right = r.ColEnd;
            }
        }

        public bool Contains(int x, int y)
        {
            // binary search for row y
            if (Runs == null || Runs.Length == 0) return false;
            int lo = 0, hi = Runs.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                int r = Runs[mid].Row;
                if (r == y)
                {
                    int s = Runs[mid].ColStart;
                    int e = Runs[mid].ColEnd;
                    if (x >= s && x < e) return true;
                    // check adjacent same-row
                    int j = mid - 1;
                    while (j >= 0 && Runs[j].Row == y)
                    {
                        if (x >= Runs[j].ColStart && x < Runs[j].ColEnd) return true;
                        j--;
                    }
                    j = mid + 1;
                    while (j < Runs.Length && Runs[j].Row == y)
                    {
                        if (x >= Runs[j].ColStart && x < Runs[j].ColEnd) return true;
                        j++;
                    }
                    return false;
                }
                if (r < y) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }
    }
}
