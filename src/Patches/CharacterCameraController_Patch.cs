﻿using Character;
using HarmonyLib;
using UnityEngine;

namespace better_mouse_mod.Patches;

/// <summary>
/// this patch is for the 1st person camera
/// </summary>
[HarmonyPatch(typeof(CharacterCameraController))]
[HarmonyPatch(nameof(CharacterCameraController.UpdateWithInput))]
public class CharacterCameraController_UpdateWithInput_Patch
{
	private static bool Prefix(ref CharacterCameraController __instance, float deltaTime, float inputPitch, float inputYaw, Lean lean)
	{
		if (!Main.MySettings.ToggleModeEnabled)
		{
			return true; //execute original function
		}
		
		__instance._lastLean = lean;
		
		//vertical
		__instance._targetPitch -= inputPitch * __instance.rotationSpeed;
		__instance._targetPitch = Mathf.Clamp(__instance._targetPitch, __instance.minVerticalAngle, __instance.maxVerticalAngle);
		//horizontal
		__instance._targetYaw += inputYaw * __instance.rotationSpeed;
		
		//rotate the camera
		__instance._transform.eulerAngles = new Vector3(__instance._targetPitch, __instance._targetYaw, 0);
		
		return false; //skip original function
	}
}