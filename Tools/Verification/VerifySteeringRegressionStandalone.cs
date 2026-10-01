using System;
using System.Reflection;
using System.Linq;
public static class SteeringRegression {
 public static int Main() {
  int passed=0,failed=0;
  var assembly=Assembly.LoadFrom("Library/SteeringVerification/Reins.EditModeTests.dll");
  foreach(var name in new[]{"Reins.Tests.ReinDrivingModelTests","Reins.Tests.CartHitPenaltyModelTests"}) {
   var type=assembly.GetType(name,true);
   foreach(var method in type.GetMethods()) {
    var attributes=method.GetCustomAttributes(false);
    var cases=attributes.Where(a=>a.GetType().Name=="TestCaseAttribute").ToArray();
    if(cases.Length==0 && !attributes.Any(a=>a.GetType().Name=="TestAttribute"))continue;
    var args=cases.Length==0 ? new object[][]{new object[0]} : cases.Select(a=>(object[])a.GetType().GetProperty("Arguments").GetValue(a,null)).ToArray();
    foreach(var values in args) {
     try { method.Invoke(Activator.CreateInstance(type),values);passed++; }
     catch(Exception e) {failed++;Console.WriteLine("FAIL "+method.Name+": "+(e.InnerException??e).Message);}
    }
   }
  }
  Console.WriteLine("{\"passed\":"+passed+",\"failed\":"+failed+",\"unityTestRunner\":false}");return failed==0?0:1;
 }
}
