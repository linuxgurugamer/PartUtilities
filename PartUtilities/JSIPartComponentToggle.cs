﻿using System;
using UnityEngine;
using System.Collections;

namespace JSIPartUtilities
{

	public class JSIPartUtilitiesRPMButtons : InternalModule
	{

		[KSPField]
		public string partComponentID = string.Empty;

		/// Controls a JSIPartComponentToggle which is part of JSIPartUtils.
		/// Unfortunately it can't return a state.
		public void ButtonTogglePartComponent (bool state)
		{
			if (!string.IsNullOrEmpty (partComponentID)) {
				var eventData = new BaseEventDetails(BaseEventDetails.Sender.USER);
				eventData.Set ("moduleID", partComponentID);
				eventData.Set ("state", state);
				eventData.Set ("objectLocal", part.gameObject);
				part.SendEvent ("JSIComponentToggle", eventData);
			}
		}

	}

	public class JSIPartComponentToggle: PartModule
	{

		[KSPField]
		public string componentName = string.Empty;

		[KSPField]
		public bool persistAfterEditor = true;

		[KSPField (isPersistant = true)]
		public bool spawned;

		[KSPField]
		public float costOfBeingEnabled = 0;

		[KSPField]
		public bool componentIsEnabled = true;

		[KSPField (isPersistant = true)]
		public bool currentState = true;

		[KSPField]
		public string moduleID = string.Empty;

		[KSPField]
		public bool controlRendering = true;
		[KSPField]
		public bool controlColliders = true;

		[KSPField]
		public bool activeInEditor = true;
		[KSPField]
		public bool activeInFlight = true;
		[KSPField]
		public bool activeWhenUnfocused = true;
		[KSPField]
		public float unfocusedActivationRange = 10;

		[KSPField]
		public bool showToggleOption = true;
		[KSPField]
		public bool showEnableDisableOption = true;

		[KSPField]
		public bool externalToEVAOnly = false;

		[KSPField]
		public string enableMenuString = string.Empty;
		[KSPField]
		public string disableMenuString = string.Empty;
		[KSPField]
		public string toggleMenuString = string.Empty;

		private string[] componentList;

		#region IPartCostModifier implementation

		public float GetModuleCost ()
		{
			return currentState ? costOfBeingEnabled : 0;
		}

		#endregion

		public override void OnStart (PartModule.StartState state)
		{
			componentList = componentName.Split (new [] { ',', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string componentText in componentList) {
				Component thatComponent = part.FindModelComponent<Component> (componentText);
				if (thatComponent == null) {
					JUtil.LogErrorMessage (this, "Target part component {0} was not found in part {1}. Selfdestructing the module...", componentText, part.name);
					Destroy (this);
				}
			}
			JUtil.LogMessage (this, "Active in part {0}, handling {1} components", part.name, componentList.Length);

			foreach (string eventName in new [] {"JSIGuiToggleComponent","JSIGuiEnableComponent","JSIGuiDisableComponent"}) {
				Events [eventName].guiActive = activeInFlight;
				Events [eventName].guiActiveEditor = activeInEditor;
				Events [eventName].guiActiveUnfocused = activeWhenUnfocused;
				Events [eventName].externalToEVAOnly = externalToEVAOnly;
				Events [eventName].unfocusedRange = unfocusedActivationRange;
			}

			if (!string.IsNullOrEmpty (enableMenuString)) {
				Events ["JSIGuiEnableComponent"].guiName = enableMenuString;
			}
			if (!string.IsNullOrEmpty (disableMenuString)) {
				Events ["JSIGuiDisableComponent"].guiName = disableMenuString;
			}
			if (!string.IsNullOrEmpty (toggleMenuString)) {
				Events ["JSIGuiToggleComponent"].guiName = toggleMenuString;
			}

			if ((HighLogic.LoadedSceneIsEditor && !spawned) || (HighLogic.LoadedSceneIsFlight && !persistAfterEditor)) {
				currentState = componentIsEnabled;
			}

			if (currentState) {
				Events ["JSIGuiEnableComponent"].active = false;
			} else {
				Events ["JSIGuiDisableComponent"].active = false;
			}

			Events ["JSIGuiToggleComponent"].active &= showToggleOption;

			if (!showEnableDisableOption) {
				Events ["JSIGuiEnableComponent"].active = false;
				Events ["JSIGuiDisableComponent"].active = false;
			}

			spawned = true;
			LoopComponents (currentState);
		}

		[KSPEvent (active = true, guiActive = false, guiActiveEditor = false)]
		public void JSIComponentToggle (BaseEventDetails data)
		{
			if (!string.IsNullOrEmpty (moduleID) && data.GetString ("moduleID") == moduleID) {
				if (data.GetGameObject ("objectLocal") == null || data.GetGameObject ("objectLocal") == part.gameObject) {
					LoopComponents (data.GetBool ("state"));
				}
			}
		}

		[KSPEvent (active = true, guiActive = true, guiActiveEditor = true, guiName = "Enable component")]
		public void JSIGuiEnableComponent ()
		{
			LoopComponents (true);
		}

		[KSPEvent (active = true, guiActive = true, guiActiveEditor = true, guiName = "Disable component")]
		public void JSIGuiDisableComponent ()
		{
			LoopComponents (false);
		}

		[KSPEvent (active = true, guiActive = true, guiActiveEditor = true, guiName = "Toggle component")]
		public void JSIGuiToggleComponent ()
		{
			LoopComponents (!currentState);
		}

		private void LoopComponents (bool newstate)
		{
			if (!spawned)
				return;

			foreach (string componentText in componentList) {
				SetState (part, componentText, newstate, controlRendering, controlColliders);
			}
			if (showEnableDisableOption) {
				if (newstate) {
					Events ["JSIGuiEnableComponent"].active = false;
					Events ["JSIGuiDisableComponent"].active = true;
				} else {
					Events ["JSIGuiDisableComponent"].active = false;
					Events ["JSIGuiEnableComponent"].active = true;
				}
			}

			currentState = newstate;

			JUtil.ForceRightclickMenuRefresh ();

			if (HighLogic.LoadedSceneIsEditor) {
				GameEvents.onEditorShipModified.Fire (EditorLogic.fetch.ship);
			}
		}

		private static void SetState (Part thatPart, string targetName, bool state, bool controlRendering, bool controlColliders)
		{
			Component thatComponent = thatPart.FindModelComponent<Component> (targetName);
			if (thatComponent != null) {
				if (controlRendering) {
					if (thatComponent.GetComponent<Renderer>() != null) {
						thatComponent.GetComponent<Renderer>().enabled = state;
					}
					foreach (Renderer thatRenderer in thatComponent.GetComponentsInChildren<Renderer>()) {
						thatRenderer.enabled = state;
					}
				}
				if (controlColliders) {
					if (thatComponent.GetComponent<Collider>() != null) {
						thatComponent.GetComponent<Collider>().enabled = state;
					}
					foreach (Collider thatCollider in thatComponent.GetComponentsInChildren<Collider>()) {
						thatCollider.enabled = state;
					}
				}
			}
		}
	}
}
