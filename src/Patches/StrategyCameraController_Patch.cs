﻿using System;
using Cameras;
using Game;
using HarmonyLib;
using Helpers;
using UI;
using UnityEngine;

namespace better_mouse_mod.Patches;

/// <summary>
/// this patch is for the 3rd person / flying camera
/// </summary>
[HarmonyPatch(typeof(StrategyCameraController))]
[HarmonyPatch(nameof(StrategyCameraController.UpdateInput))]
public class StrategyCameraController_UpdateInput_Patch
{
	// this should probably be a transpile but i haven't learned how to make those yet
	private static bool Prefix(ref StrategyCameraController __instance)
	{
		// ======= this is unchanged: ======
		
		__instance._distanceInput = 0.0f;
    __instance._angleXInput = 0.0f;
    __instance._angleYInput = 0.0f;
    
    // WASD and QE
    Vector3 movement = GameInput.shared.GetMovement(__instance.normalSpeed, __instance.fastSpeed, __instance.fasterSpeed);
    __instance._movementInput = new Vector3(movement.x, 0.0f, movement.z);
    
    // ======= this is changed: ======
    
    // this is moving the camera with Q and E
    if (Main.MySettings.DisableCameraSmoothing)
    {
	    __instance._angleYInput += movement.y / 60f;
    }
    else
    {
	    __instance._angleYInput += movement.y / 5f;
    }

    // ======= this is unchanged: ======
    
    bool doPanning = false;
    bool doLookEnable = false;
    if (!GameInput.IsMouseOverUI(out TooltipInfo _, out string _))
    {
	    //why don't they use GameInput for this?
	    doPanning = Input.GetMouseButtonDown(Stuff.LEFT_MOUSE_BUTTON) && !ObjectPicker.Shared.IsOverObject;
      doLookEnable = GameInput.shared.LookEnableDown;
    }

    if (GameInput.IsMouseOverGameWindow() && Math.Abs(Input.mouseScrollDelta.y) > 1.0 / 1000.0)
    {
	    __instance._distanceInput = -Input.mouseScrollDelta.y;
    }

    // ======= this is changed: ======
    
    if (!Main.MySettings.DisableLeftClickPanning)
    {
	    HandlePanning(ref __instance, doPanning);
    }
    
    if (doLookEnable)
    {
	    if (Main.MySettings.ToggleModeEnabled)
	    {
		    __instance._rotateStarted = !__instance._rotateStarted;

		    if (__instance._rotateStarted)
		    {
			    __instance._rotateStartPosition = Input.mousePosition;
			    if (Main.MySettings.DisableCameraSmoothing)
			    {
				    Cursor.lockState = CursorLockMode.Locked;
			    }
		    }
		    else if (Main.MySettings.DisableCameraSmoothing)
		    {
			    Cursor.lockState = CursorLockMode.None;
		    }
	    }
	    else
	    {
		    __instance._rotateStartPosition = Input.mousePosition;
		    __instance._rotateStarted = true;
	    }
    }

    if (__instance._rotateStarted && !Main.MySettings.ToggleModeEnabled && !Input.GetMouseButton(Stuff.RIGHT_MOUSE_BUTTON))
    {
		  __instance._rotateStarted = false;
    }

    if (__instance._rotateStarted)
    {
	    if (Main.MySettings.DisableCameraSmoothing)
	    {
		    var lookInput = GameInput.shared.LookDelta;
		    //y and x intentionally reversed
		    __instance._angleYInput = lookInput.x;
		    __instance._angleXInput = -lookInput.y;
	    }
	    else
	    {
		    __instance._rotateCurrentPosition = Input.mousePosition;
		    var vector3 = __instance._rotateStartPosition - __instance._rotateCurrentPosition;
		    
		    __instance._rotateStartPosition = __instance._rotateCurrentPosition;
		    __instance._angleYInput = (float) (-(double) vector3.x * 0.85000002384185791);
		    __instance._angleXInput = vector3.y * 0.5f;
	    }
    }

		return false; //skip original function
	}

	/// <summary>
	/// camera panning with left mouse button
	/// </summary>
	/// <param name="__instance"></param>
	/// <param name="leftMouseButtonClicked"></param>
	private static void HandlePanning(ref StrategyCameraController __instance, bool leftMouseButtonClicked)
	{
		bool holdingLeftMouse = Input.GetMouseButton(Stuff.LEFT_MOUSE_BUTTON);
		Vector3 point;

		if (leftMouseButtonClicked && __instance.RayPointFromMouse(out point))
		{
			__instance._panStartCameraPosition = __instance.CameraContainer.position;
			__instance._panStartPosition = point;
			__instance._panStartTarget = __instance._targetPosition;
			__instance._panPlane = new Plane(Vector3.up, point);
			__instance.FollowCar = null;
		}
		else if (holdingLeftMouse && __instance._panStartPosition.HasValue)
		{
			Vector3 vector3 = __instance.CameraContainer.position - __instance._panStartCameraPosition;
			Ray mouseRay = __instance.GetMouseRay();
			mouseRay.origin -= vector3;
			float enter;
			__instance._panPlane.Raycast(mouseRay, out enter);
			__instance._moveToTarget = __instance.SnapToGround(
				__instance._panStartTarget + (__instance._panStartPosition.Value - mouseRay.GetPoint(enter)));
			__instance._moveTimer = 0.0f;
		}
		else
		{
			__instance._panStartPosition = new Vector3?();
		}
	}

	[HarmonyPatch(typeof(StrategyCameraController))]
	[HarmonyPatch(nameof(StrategyCameraController.UpdateCameraPosition))]
	public class StrategyCameraController_UpdateCameraPosition_Patch
	{
		private static bool Prefix(ref StrategyCameraController __instance, bool immediate)
		{
			if (!Main.MySettings.DisableCameraSmoothing)
			{
				return true; //execute original function
			}
			
			// ============== unchanged: ==============
			
			float fixedDeltaTime = Time.fixedDeltaTime;
      bool followingCar = __instance.FollowCar != null;
      
      __instance._targetHeight = Mathf.Lerp(__instance._targetHeight, followingCar ? __instance.targetHeightFollow : __instance.targetHeightFree, fixedDeltaTime * 5f);
      
      var vector3_1 = __instance.distanceToSpeed.Evaluate(__instance._distance) * fixedDeltaTime * __instance._movementInput;
      
      float t = fixedDeltaTime * 5f;
      vector3_1.y = 0.0f;
      __instance._movementVelocity = Vector3.Lerp(__instance._movementVelocity, __instance.transform.rotation.OnlyEulerY() * vector3_1, t);
      __instance._targetPosition += 50f * fixedDeltaTime * __instance._movementVelocity;
      
      if (__instance._movementVelocity.magnitude > 1.0 / 1000.0)
      {
	      __instance._moveToTarget = new Vector3?();
      }

      if (__instance._moveToTarget.HasValue)
      {
        __instance._moveTimer += fixedDeltaTime;
        __instance._targetPosition = Vector3.Lerp(__instance._targetPosition, __instance._moveToTarget.Value, fixedDeltaTime * 10f);
        if (__instance._moveTimer > 5.0)
        {
          __instance._targetPosition = __instance._moveToTarget.Value;
          __instance._moveToTarget = new Vector3?();
        }
      }
      
      float extraRotationY = 0.0f;
      if (__instance._movementVelocity.magnitude > 1.0 / 1000.0)
      {
        __instance._targetPosition = __instance.SnapToGround(__instance._targetPosition);
        __instance.FollowCar = null;
      }
      else if (followingCar)
      {
        var vector3_2 = __instance._targetHeight * Vector3.up;
        (Vector3 position, Quaternion rotation) = __instance.FollowCar.GetMoverTargetPositionRotation();
        __instance._targetPosition = position + vector3_2;
        Quaternion quaternion = Quaternion.Inverse(__instance._followCarInitialRotation);
        extraRotationY = (rotation * quaternion).eulerAngles.y;
      }
      
      __instance._extraRotationY = extraRotationY;
      
      __instance._distanceVelocity = Mathf.Lerp(__instance._distanceVelocity, __instance._distanceInput, t);
      __instance._distance += __instance._distanceVelocity * __instance.ZoomDelta * fixedDeltaTime;
      __instance._distance = Mathf.Clamp(__instance._distance, 1f, 500f);
      
      // ============== changed: ==============
      
      __instance._angleY += __instance._angleYInput * Preferences.MouseLookSpeed;
      __instance._angleX += __instance._angleXInput * Preferences.MouseLookSpeed;
      
      // ============== end of changed code ==============
      
      __instance._angleX = Mathf.Clamp(__instance._angleX, -30f, 90f);
      
      __instance.ClampPitchToGround(__instance.transform.position, 1f);
      __instance.UpdatePosition(immediate);
      
      return false; //skip original function
		}
	}
}

/// <summary>
/// DisableCameraSmoothing
/// </summary>
[HarmonyPatch(typeof(StrategyCameraController))]
[HarmonyPatch(nameof(StrategyCameraController.UpdatePosition))]
public class StrategyCameraController_UpdatePosition_Patch
{
	private static bool Prefix(ref StrategyCameraController __instance)
	{
		if (!Main.MySettings.DisableCameraSmoothing)
		{
			return true; //execute original function
		}
		
		var rotation = Quaternion.Euler(__instance._angleX, __instance._angleY + __instance._extraRotationY, 0.0f);
		Vector3 position = __instance._targetPosition + rotation * (Vector3.back * __instance._distance);
		
		var transform = __instance.transform;
		transform.position = position;
		transform.rotation = rotation;

		return false;
	}
}
