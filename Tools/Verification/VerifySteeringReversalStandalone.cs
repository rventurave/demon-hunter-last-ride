using System;
using Reins;
using UnityEngine;
public static class SteeringCheck {
 static int count;
 static void Check(bool result,string name) { if(!result)throw new Exception(name); count++; }
 public static int Main() {
  try {
   foreach(int sign in new[]{-1,1}) {
    var m=new ReinGestureStateMachine(laneThreshold:.15f,brakeEnabled:false);
    Check(m.Step(true,new Vector3(.05f,0,0),.016f).Kind==ReinGestureKind.None,"Noise");
    Check(m.Step(true,new Vector3(sign*.2f,0,.3f),.016f).Direction==sign,"First direction");
    m.Step(true,new Vector3(0,0,.3f),.016f);
    Check(m.Step(true,new Vector3(-sign*.2f,0,.3f),.016f).Direction==-sign,"Reversal during cooldown with backward offset");
    for(int i=0;i<50;i++) Check(m.Step(true,new Vector3(-sign*.2f,0,.3f),.016f).Kind==ReinGestureKind.None,"Held input spam");
    Check(m.Step(true,new Vector3(sign*.2f,0,.3f),.016f).Direction==sign,"Reversal skipping neutral sample");
    var lane=new LaneTransitionModel(); lane.Begin(0,sign,2.8f,.8f); float halfway=lane.Step(.2f);
    lane.Begin(halfway,-sign,2.8f,.8f); Check(Math.Abs(lane.CurrentX-halfway)<.001f,"Reversal discontinuity");
    Check(sign*lane.Step(.8f)<0,"Reversal end");
   }
   var lash=new ReinGestureStateMachine(laneThreshold:.15f,brakeEnabled:false);
   lash.Step(true,Vector3.up*.2f,.016f);
   Check(lash.Step(true,Vector3.zero,.016f).Kind==ReinGestureKind.Accelerate,"Lash lost");
   Check(lash.Step(true,Vector3.right*.2f,.016f).Kind==ReinGestureKind.None,"Lash cooldown changed");
   Console.WriteLine("{\"passed\":true,\"assertions\":"+count+",\"realUnityMathTypes\":true,\"editorPlayTested\":false}"); return 0;
  } catch(Exception e) { Console.WriteLine("FAIL: "+e.Message);return 1; }
 }
}
