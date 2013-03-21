/*
 * Created by SharpDevelop.
 * User: Bernhard
 * Date: 21.03.2013
 * Time: 01:34
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

public class RCCPartlessLoader : KSP.Testing.UnitTest {
	public RCCPartlessLoader() : base() {
		//Called at the first loading screen
		//When you start the game.
		RCCLoader.Initialize();
	}
}

public static class RCCLoader {
	private static UnityEngine.GameObject MyMonobehaviourObject;

	public static void Initialize() {
		MyMonobehaviourObject = new UnityEngine.GameObject("ModuleAttacherLoader", new Type[] {typeof(RoverCruiseControlAttacher)});
		UnityEngine.GameObject.DontDestroyOnLoad(MyMonobehaviourObject);
	}
}

public class RoverCruiseControlAttacher : UnityEngine.MonoBehaviour {
	private Vessel last;
	
	public void Update() {
		if (!HighLogic.LoadedSceneIsFlight) {
			return;
		}
		
		var ship = FlightGlobals.ActiveVessel;
		
//		try {
		if (ship != null && ship.state == Vessel.State.ACTIVE && last != ship) {
			foreach (Part p in ship.Parts) {
				if (p.Modules.Contains("ModuleCommand") && !p.Modules.Contains("RoverCruiseControl")) {
					p.AddModule("RoverCruiseControl");
					RoverCruiseControl.print("attached to " + p.name);
				}
			}
			last = ship;
		}
//		}
//		catch (Exception ex) {
//			print(ex.Message);
//		}
	}

}

public class RoverCruiseControl : PartModule {
	public static void print(string message) {
		MonoBehaviour.print("RCC: " + message);
	}
	
	public bool Active = false;
	public bool Wheels = false;
	public float upperSpeedLimit = 0;
	public float lowerSpeedLimit = 1000;
	private int parts;
	private List<RoverCruiseControl> controllers = new List<RoverCruiseControl>();
	
	[KSPEvent(guiActive = false, guiName = "Activate Cruise Control", active = true)]
	public void Activate() {
		ChangeState(true);
		UpdateOthers();
	}

	[KSPEvent(guiActive = false, guiName = "Deactivate Cruise Control", active = false)]
	public void Deactivate() {
		ChangeState(false);
		UpdateOthers();
	}

	[KSPAction("Toggle Cruise Control")]
	public void Toggle(KSPActionParam param) {
		ChangeState(!Active);
		UpdateOthers();
	}

	[KSPAction("Activate Cruise Control")]
	public void Activate(KSPActionParam param) {
		Activate();
	}

	[KSPAction("Deactivate Cruise Control")]
	public void Deactivate(KSPActionParam param) {
		Deactivate();
	}
	
	public void ChangeState(bool NowActive) {
		Active = NowActive;
		Events["Activate"].active = !Active;
		Events["Deactivate"].active = Active;
	}
	
	private void UpdateOthers() {
		controllers.ForEach(c => c.ChangeState(Active));
	}
	
	private void ScanVessel() {
		controllers.Clear();
		upperSpeedLimit = 0;
		lowerSpeedLimit = 1000;
		parts = vessel.Parts.Count;
		Wheels = false;
		
		foreach (Part p in vessel.Parts) {
			foreach (PartModule pm in p.Modules) {
				if (pm is ModuleWheel) {
					var wheel = (ModuleWheel)pm;
					float speed = FindTorque(wheel);
//					print("Speed: " + speed);
					upperSpeedLimit = Mathf.Max(upperSpeedLimit, speed);
					lowerSpeedLimit = Mathf.Min(lowerSpeedLimit, speed);
					Wheels = true;
				}
				else if (pm is RoverCruiseControl) {
					controllers.Add((RoverCruiseControl)pm);
				}
			}
		}
		
		if (controllers.Count > 0 && this == controllers[0]) {
			print("Controllers: " + controllers.Count);
		}
		
		Events["Activate"].guiActive = Events["Deactivate"].guiActive = Wheels;
	}
	
	public float FindTorque(ModuleWheel w) {
		var fc = w.torqueCurve;
		var t = 0f;
		var res = fc.Evaluate(t);
		
		while (res > 0) {
			t += 10;
			res = fc.Evaluate(t);
		}
		
		while (res == 0) {
			t--;
			res = fc.Evaluate(t);
		}
		
		while (res > 0) {
			t += 0.1f;
			res = fc.Evaluate(t);
		}
		
		return t;
	}
	
	private void Init() {
		if (vessel != null) {
			vessel.OnFlyByWire -= OnFlyByWire;
			vessel.OnFlyByWire += OnFlyByWire;
			ScanVessel();
		}
	}
	
//	public override void OnAwake() {
//		Init();
//		base.OnAwake();
//	}
//
//	public override void OnActive() {
//		Init();
//	}
//
//	public override void OnInactive() {
//		Init();
//	}
//
	public override void OnStart(PartModule.StartState state) {
		print("Start: " + vessel.ToString());
		Init();
		base.OnStart(state);
	}
	
	public void OnDestroy() {
		if (vessel != null) {
			vessel.OnFlyByWire -= OnFlyByWire;
		}
	}
	
	public void Update() {
		if (vessel.Parts.Count != parts || controllers.Count == 0) { Init(); }
		
		if (!Wheels || controllers.Count == 0 || this != controllers[0]) { return; }
		
		if (Input.GetKeyUp(KeyCode.O)) {
			Toggle(null);
			if (Active) {
				var speed = (float)vessel.horizontalSrfSpeed;
				FlightInputHandler.state.mainThrottle = speed / upperSpeedLimit;
			}
		}
	}
	
	public void OnFlyByWire(FlightCtrlState fs) {
		if (!Wheels || !Active || this != controllers[0]) { return; }
		
		var speed = (float)vessel.horizontalSrfSpeed;

		if (fs.wheelThrottle == 0) {
			var diff = ((upperSpeedLimit * fs.mainThrottle) - speed);
			fs.wheelThrottle = Mathf.Clamp(diff * 5, 0, 1);
			vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, diff <= 0 || GameSettings.BRAKES.GetKey());
		} else {
			vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, GameSettings.BRAKES.GetKey());
		}
	}
}
