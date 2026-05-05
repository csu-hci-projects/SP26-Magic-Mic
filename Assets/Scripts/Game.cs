using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR;

using static Unity.Mathematics.math;

using Random = UnityEngine.Random;

public class Game : MonoBehaviour
{
	enum GameState
	{
		SelectModality,
		CalibratingQuiet,
		CalibratingLoud,
		Playing,
		Paused,
		Ended
	}

	[SerializeField]
	MazeVisualization visualization;

	[SerializeField]
	int2 mazeSize = int2(20, 20);

	[SerializeField, Tooltip("Use zero for random seed.")]
	int seed;

	[SerializeField, Range(0f, 1f)]
	float
		pickLastProbability = 0.5f,
		openDeadEndProbability = 0.5f,
		openArbitraryProbability = 0.5f;

	[SerializeField]
	Player player;

	[SerializeField]
	Agent[] agents;

	[SerializeField]
	GameUiController ui;

	[SerializeField, Tooltip("Old prompt text kept as a fallback until the new UI is wired.")]
	TextMeshPro displayText;

	Maze maze;

	Scent scent;

	GameState state;

	GameState stateBeforePause;

	MovementModality activeModality = MovementModality.Controller;

	MazeCellObject[] cellObjects;

	InputDevice rightHandDevice;

	readonly List<InputDevice> inputDevices = new List<InputDevice>();

	bool wasAButtonPressed;

	bool wasBButtonPressed;

	bool isRecordingCalibration;

	bool isWaitingForFallbackStart;

	bool hasActiveGameData;

	void Start ()
	{
		if (displayText != null)
		{
			displayText.gameObject.SetActive(false);
		}

		EnsureUi();
		ShowModalitySelection();
	}

	void EnsureUi ()
	{
		if (ui == null)
		{
			ui = FindFirstObjectByType<GameUiController>(FindObjectsInactive.Include);
		}

		if (ui == null)
		{
			ui = gameObject.AddComponent<GameUiController>();
		}

		ui.Initialize(player != null ? player.ViewTransform : null);
	}

	void StartNewGame (MovementModality modality)
	{
		LeaveCurrentGame(false);
		state = GameState.Playing;
		activeModality = modality;
		isRecordingCalibration = false;
		isWaitingForFallbackStart = false;
		player.SetMovementModality(modality);

		maze = new Maze(mazeSize);
		scent = new Scent(maze);
		hasActiveGameData = true;
		new FindDiagonalPassagesJob
		{
			maze = maze
		}.ScheduleParallel(
			maze.Length, maze.SizeEW, new GenerateMazeJob
			{
				maze = maze,
				seed = seed != 0 ? seed : Random.Range(1, int.MaxValue),
				pickLastProbability = pickLastProbability,
				openDeadEndProbability = openDeadEndProbability,
				openArbitraryProbability = openArbitraryProbability
			}.Schedule()
		).Complete();

		if (cellObjects == null || cellObjects.Length != maze.Length)
		{
			cellObjects = new MazeCellObject[maze.Length];
		}
		visualization.Visualize(maze, cellObjects);

		if (seed != 0)
		{
			Random.InitState(seed);
		}

		player.StartNewGame(maze.CoordinatesToWorldPosition(
			int2(Random.Range(0, mazeSize.x / 4), Random.Range(0, mazeSize.y / 4))
		));

		int2 halfSize = mazeSize / 2;
		for (int i = 0; i < agents.Length; i++)
		{
			var coordinates =
				int2(Random.Range(0, mazeSize.x), Random.Range(0, mazeSize.y));
			if (coordinates.x < halfSize.x && coordinates.y < halfSize.y)
			{
				if (Random.value < 0.5f)
				{
					coordinates.x += halfSize.x;
				}
				else
				{
					coordinates.y += halfSize.y;
				}
			}
			agents[i].StartNewGame(maze, coordinates);
		}

		ui.ShowHud(modality);
		UpdateVoiceHud();
	}

	void Update ()
	{
		bool aButtonDown =
			GetButtonDown(CommonUsages.primaryButton, ref wasAButtonPressed);
		bool bButtonDown =
			GetButtonDown(CommonUsages.secondaryButton, ref wasBButtonPressed);

		switch (state)
		{
			case GameState.SelectModality:
				UpdateModalitySelection(aButtonDown, bButtonDown);
				break;
			case GameState.CalibratingQuiet:
				UpdateQuietCalibration(aButtonDown, bButtonDown);
				break;
			case GameState.CalibratingLoud:
				UpdateLoudCalibration(aButtonDown, bButtonDown);
				break;
			case GameState.Playing:
				UpdatePlaying(bButtonDown);
				break;
			case GameState.Paused:
				UpdatePause(aButtonDown, bButtonDown);
				break;
			case GameState.Ended:
				if (aButtonDown)
				{
					ShowModalitySelection();
				}
				break;
		}
	}

	void UpdateModalitySelection (bool aButtonDown, bool bButtonDown)
	{
		if (aButtonDown)
		{
			StartNewGame(MovementModality.Controller);
			UpdateGame();
		}
		else if (bButtonDown)
		{
			player.SetMovementModality(MovementModality.Voice);
			ShowQuietCalibrationPrompt();
		}
	}

	void UpdateQuietCalibration (bool aButtonDown, bool bButtonDown)
	{
		if (!isRecordingCalibration)
		{
			UpdateCalibrationMeter("Ready");
			if (bButtonDown)
			{
				ShowModalitySelection();
			}
			else if (aButtonDown)
			{
				StartQuietCalibration();
			}
			return;
		}

		if (player.UpdateVoiceCalibration())
		{
			ShowLoudCalibrationPrompt();
			return;
		}
		UpdateCalibrationMeter("Recording quiet");
	}

	void UpdateLoudCalibration (bool aButtonDown, bool bButtonDown)
	{
		if (isWaitingForFallbackStart)
		{
			UpdateFallbackPrompt();
			if (bButtonDown)
			{
				ShowQuietCalibrationPrompt();
			}
			else if (aButtonDown)
			{
				StartNewGame(MovementModality.Voice);
				UpdateGame();
			}
			return;
		}

		if (!isRecordingCalibration)
		{
			UpdateCalibrationMeter("Ready");
			if (bButtonDown)
			{
				ShowQuietCalibrationPrompt();
			}
			else if (aButtonDown)
			{
				StartLoudCalibration();
			}
			return;
		}

		if (player.UpdateVoiceCalibration())
		{
			if (player.HasUsableVoiceCalibration())
			{
				StartNewGame(MovementModality.Voice);
				UpdateGame();
			}
			else
			{
				player.UseFallbackVoiceCalibration();
				isRecordingCalibration = false;
				isWaitingForFallbackStart = true;
				UpdateFallbackPrompt();
			}
			return;
		}
		UpdateCalibrationMeter("Recording loud");
	}

	void UpdatePlaying (bool bButtonDown)
	{
		if (bButtonDown)
		{
			ShowPauseMenu();
			return;
		}

		UpdateGame();
		UpdateVoiceHud();
	}

	void UpdatePause (bool aButtonDown, bool bButtonDown)
	{
		if (aButtonDown)
		{
			state = stateBeforePause;
			player.SetMovementPaused(false);
			ui.ShowHud(activeModality);
			UpdateVoiceHud();
		}
		else if (bButtonDown)
		{
			LeaveCurrentGame(true);
			ShowModalitySelection();
		}
	}

	void ShowModalitySelection ()
	{
		LeaveCurrentGame(false);
		state = GameState.SelectModality;
		activeModality = MovementModality.Controller;
		isRecordingCalibration = false;
		isWaitingForFallbackStart = false;
		player.SetMovementModality(MovementModality.Controller);
		ui.ShowMainMenu();
	}

	void ShowQuietCalibrationPrompt ()
	{
		state = GameState.CalibratingQuiet;
		isRecordingCalibration = false;
		isWaitingForFallbackStart = false;
		ui.ShowCalibrationQuiet();
		UpdateCalibrationMeter("Ready");
	}

	void ShowLoudCalibrationPrompt ()
	{
		state = GameState.CalibratingLoud;
		isRecordingCalibration = false;
		isWaitingForFallbackStart = false;
		ui.ShowCalibrationLoud(player.QuietVolume);
		UpdateCalibrationMeter("Ready");
	}

	void StartQuietCalibration ()
	{
		if (!player.BeginVoiceCalibration(VoiceCalibrationStep.Quiet))
		{
			ShowMicrophoneError();
			return;
		}
		isRecordingCalibration = true;
		ui.ShowCalibrationRecording("Recording quiet", "Stay as quiet as you can.");
		UpdateCalibrationMeter("Recording quiet");
	}

	void StartLoudCalibration ()
	{
		if (!player.BeginVoiceCalibration(VoiceCalibrationStep.Loud))
		{
			ShowMicrophoneError();
			return;
		}
		isRecordingCalibration = true;
		ui.ShowCalibrationRecording("Recording loud", "Speak clearly and loudly.");
		UpdateCalibrationMeter("Recording loud");
	}

	void ShowMicrophoneError ()
	{
		state = GameState.SelectModality;
		isRecordingCalibration = false;
		isWaitingForFallbackStart = false;
		ui.ShowMicrophoneError(player.MicrophoneStatus);
	}

	void UpdateCalibrationMeter (string label)
	{
		ui.UpdateCalibrationMeter(
			player.PreviewMicrophoneVolume(),
			player.QuietVolume,
			player.LoudVolume,
			player.MicrophoneStatus,
			label
		);
	}

	void UpdateFallbackPrompt ()
	{
		ui.ShowCalibrationFallback(
			player.PreviewMicrophoneVolume(),
			player.QuietVolume,
			player.LoudVolume,
			player.MicrophoneStatus
		);
	}

	void UpdateVoiceHud ()
	{
		if (activeModality != MovementModality.Voice)
		{
			return;
		}

		ui.UpdateHudVoiceMeter(
			player.PreviewMicrophoneVolume(),
			player.QuietVolume,
			player.LoudVolume,
			player.MicrophoneStatus
		);
	}

	void ShowPauseMenu ()
	{
		stateBeforePause = state;
		state = GameState.Paused;
		player.SetMovementPaused(true);
		ui.ShowPause(activeModality);
	}

	void UpdateGame ()
	{
		if (!hasActiveGameData)
		{
			return;
		}

		Vector3 playerPosition = player.Move();
		NativeArray<float> currentScent = scent.Disperse(maze, playerPosition);
		for (int i = 0; i < agents.Length; i++)
		{
			Vector3 agentPosition = agents[i].Move(currentScent);
			if (
				new Vector2(
					agentPosition.x - playerPosition.x,
					agentPosition.z - playerPosition.z
				).sqrMagnitude < 1f
			)
			{
				EndGame(agents[i].TriggerMessage);
				return;
			}
		}
	}

	void EndGame (string message)
	{
		state = GameState.Ended;
		player.EndGame();
		CleanupCurrentGame();
		ui.ShowEnd(message);
	}

	void LeaveCurrentGame (bool stopPlayer)
	{
		if (stopPlayer)
		{
			player.EndGame();
		}
		CleanupCurrentGame();
	}

	void CleanupCurrentGame ()
	{
		for (int i = 0; i < agents.Length; i++)
		{
			agents[i].EndGame();
		}

		if (cellObjects != null)
		{
			for (int i = 0; i < cellObjects.Length; i++)
			{
				if (cellObjects[i] != null)
				{
					cellObjects[i].Recycle();
					cellObjects[i] = null;
				}
			}
		}

		DisposeGameData();
	}

	void OnDestroy ()
	{
		player.EndGame();
		DisposeGameData();
	}

	void DisposeGameData ()
	{
		if (!hasActiveGameData)
		{
			return;
		}

		maze.Dispose();
		scent.Dispose();
		hasActiveGameData = false;
	}

	bool GetButtonDown (InputFeatureUsage<bool> button, ref bool wasPressed)
	{
		bool isPressed = TryGetRightHandButton(button, out bool value) && value;
		bool isDown = isPressed && !wasPressed;
		wasPressed = isPressed;
		return isDown;
	}

	bool TryGetRightHandButton (InputFeatureUsage<bool> button, out bool value)
	{
		value = false;
		if (!rightHandDevice.isValid)
		{
			InputDevices.GetDevicesAtXRNode(XRNode.RightHand, inputDevices);
			rightHandDevice = inputDevices.Count > 0 ?
				inputDevices[0] : default(InputDevice);
		}
		return rightHandDevice.isValid &&
			rightHandDevice.TryGetFeatureValue(button, out value);
	}
}
