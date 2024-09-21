namespace P5R_Packager.DataTool.CrosswordImport
{
    public sealed class CRWDCell
    {
        public CRWDCell(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; }

        public int Column { get; }

        public bool Available { get; set; } = false;

        public ushort? Char { get; set; } = null;

        public int? SideQuestion { get; set; } = null;

        public CRWDDirection? SideDirection { get; set; } = null;

        public bool MainQuestion { get; set; } = false;

        public CRWDDirection? MainDirection { get; set; } = null;

        public bool IsValid()
        {
            if (!Available)
            {
                if (Char.HasValue ||
                    SideQuestion.HasValue ||
                    SideDirection.HasValue ||
                    MainQuestion ||
                    MainDirection.HasValue)
                    return false;
            }
            else
            {
                if (!Char.HasValue)
                    return false;

                if (!SideQuestion.HasValue && SideDirection.HasValue)
                    return false;

                if (SideQuestion.HasValue && !SideDirection.HasValue)
                    return false;

                if (!MainQuestion && MainDirection.HasValue)
                    return false;

                if (MainQuestion && !MainDirection.HasValue)
                    return false;
            }

            return true;
        }
    }
}