namespace QL_PhongTro.ViewModels;
public sealed record FinanceBar(string Label, long Issued, long Paid);
public sealed class DashboardFinance
{
    public bool Available { get; set; }
    public long TotalDebt { get; set; }
    public int UnpaidCount { get; set; }
    public int OverdueCount { get; set; }
    public long Issued { get; set; }
    public long Paid { get; set; }
    public long Deposit { get; set; }
    public long Remaining { get; set; }
    public int InvoiceCount { get; set; }
    public List<FinanceBar> Months { get; set; } = [];
    public Dictionary<int, FinanceBar> Buildings { get; set; } = [];
}
