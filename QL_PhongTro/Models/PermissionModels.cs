namespace QL_PhongTro.Models;

public class AppRole
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}
public class AppModule
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string GroupName { get; set; } = "";
    public int SortOrder { get; set; }
}
public class RolePermission
{
    public string RoleCode { get; set; } = "";
    public string ModuleCode { get; set; } = "";
    public string AccessLevel { get; set; } = "NONE";
    public bool OwnDataOnly { get; set; }
}

