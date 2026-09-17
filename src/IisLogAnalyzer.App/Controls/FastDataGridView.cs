namespace IisLogAnalyzer.App.Controls;

/// <summary>DataGridView with double-buffering enabled to avoid flicker on large result sets.</summary>
public sealed class FastDataGridView : DataGridView
{
    public FastDataGridView()
    {
        DoubleBuffered = true;
        RowHeadersVisible = false;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        ReadOnly = true;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        BackgroundColor = Color.White;
        BorderStyle = BorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        EnableHeadersVisualStyles = false;
        ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(37, 47, 63);
        ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        ColumnHeadersDefaultCellStyle.Padding = new Padding(4);
        DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        DefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 228, 247);
        DefaultCellStyle.SelectionForeColor = Color.Black;
        AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 248, 250);
        RowTemplate.Height = 26;
    }
}
