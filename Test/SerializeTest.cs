using NewLife.Serialization;
using System.Runtime.Serialization;

class TestClass {
    public string Model { get; set; } = "test";
    [DataMember(Name = "result_format")]
    public string ResultFormat { get; set; } = "message";
    public string? NullProp { get; set; }
}

var obj = new TestClass();
var json = obj.ToJson();
Console.WriteLine("FastJson: " + json);
