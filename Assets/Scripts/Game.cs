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
	TextMeshPro displayText;

	Maze maze;

	Scent scent;

	GameState state;

	MazeCellObject[] cellObjects;

	InputDevice rightHandDevice;

	readonly List<InputDevice> inputDevices = new List<InputDevice>();

	bool wasPrimaryButtonPressed;

	bool wasSecondaryButtonPressed;

	bool isRecordingCalibration;

	bool isWaitingForFallbackStart;

	void Start ()
	{
		ShowModalitySelection();
	}

	void StartNewGame (MovementModality modality)
	{
		state = GameState.Playing;
		player.SetMovementModality(modality);
		displayText.gameObject.SetActive(false);
		maze = new Maze(mazeSize);
		scent = new Scent(maze);
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
	}

	void Update ()
	{
		bool primaryButtonDown =
			GetButtonDown(CommonUsages.primaryButton, ref wasPrimaryButtonPressed);
		bool secondaryButtonDown =
			GetButtonDown(CommonUsages.secondaryButton, ref wasSecondaryButtonPressed);

		switch (state)
		{
			case GameState.SelectModality:
				UpdateModalitySelection(primaryButtonDown, secondaryButtonDown);
				break;
			case GameState.CalibratingQuiet:
				UpdateQuietCalibration(primaryButtonDown, secondaryButtonDown);
				break;
			case GameState.CalibratingLoud:
				UpdateLoudCalibration(primaryButtonDown, secondaryButtonDown);
				break;
			case GameState.Playing:
				UpdateGame();
				break;
			case GameState.Ended:
				if (primaryButtonDown)
				{
					ShowModalitySelection();
				}
				break;
		}
	}

	void UpdateModalitySelection (bool primaryButtonDown, bool secondaryButtonDown)
	{
		if (primaryButtonDown)
		{
			StartNewGame(MovementModality.Controller);
			UpdateGame();
		}
		else if (secondaryButtonDown)
		{
			player.SetMovementModality(MovementModality.Voice);
			ShowQuietCalibrationPrompt();
		}
	}

	void UpdateQuietCalibration (bool primaryButtonDown, bool secondaryButtonDown)
	{
		if (!isRecordingCalibration)
		{
			if (secondaryButtonDown)
			{
				ShowModalitySelection();
			}
			else if (primaryButtonDown)
			{
				StartQuietCalibration();
			}
			return;
		}

		if (player.UpdateVoiceCalibration())
		{
			ShowLoudCalibrationPrompt();
		}
	}

	void UpdateLoudCalibration (bool primaryButtonDown, bool secondaryButtonDown)
	{
		if (isWaitingForFallbackStart)
		{
			if (secondaryButtonDown)
			{
				ShowQuietCalibrationPrompt();
			}
			else if (primaryButtonDown)
			{
				StartNewGame(MovementModality.Voice);
				UpdateGame();
			}
			return;
		}

		if (!isRecordingCalibration)
		{
			if (secondaryButtonDown)
			{
				ShowQuietCalibrationPrompt();
			}
			else if (primaryButtonDown)
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
				displayText.text =
					"LOUD VOICE WAS TOO CLOSE TO QUIET\n" +
					"PRIMARY: START WITH FALLBACK\n" +
					"SECONDARY: RECALIBRATE";
				displayText.gameObject.SetActive(true);
			}
		}
	}

	void ShowModalitySelection ()
	{
		state = GameState.SelectModality;
		isRecordingCalibration = false;
		isWaitingForFallbackStart = false;
		player.SetMovementModality(MovementModality.Controller);
		displayText.text =
			"SELECT MOVEMENT\n" +
			"PRIMARY: CONTROLLER\n" +
			"SECONDARY: VOICE";
		displayText.gameObject.SetActive(true);
	}

	void ShowQuietCalibrationPrompt ()
	{
		state = GameState.CalibratingQuiet;
		isRecordingCalibration = false;
		isWaitingForFallbackStart = false;
		displayText.text =
			"VOICE CALIBRATION\n" +
			"STAY QUIET\n" +
			"PRIMARY: RECORD QUIET\n" +
			"SECONDARY: BACK";
		displayText.gameObject.SetActive(true);
	}

	void ShowLoudCalibrationPrompt ()
	{
		state = GameState.CalibratingLoud;
		isRecordingCalibration = false;
		isWaitingForFallbackStart = false;
		displayText.text =
			"VOICE CALIBRATION\n" +
			"SPEAK LOUDLY\n" +
			"PRIMARY: RECORD LOUD\n" +
			"SECONDARY: RECALIBRATE";
		displayText.gameObject.SetActive(true);
	}

	void StartQuietCalibration ()
	{
		if (!player.BeginVoiceCalibration(VoiceCalibrationStep.Quiet))
		{
			displayText.text =
				"NO MICROPHONE FOUND\n" +
				"CHECK PERMISSION AND DEVICE\n" +
				"PRIMARY: CONTROLLER\n" +
				"SECONDARY: TRY VOICE AGAIN";
			state = GameState.SelectModality;
			return;
		}
		isRecordingCalibration = true;
		displayText.text = "RECORDING QUIET...";
	}

	void StartLoudCalibration ()
	{
		if (!player.BeginVoiceCalibration(VoiceCalibrationStep.Loud))
		{
			displayText.text =
				"NO MICROPHONE FOUND\n" +
				"CHECK PERMISSION AND DEVICE\n" +
				"PRIMARY: CONTROLLER\n" +
				"SECONDARY: TRY VOICE AGAIN";
			state = GameState.SelectModality;
			return;
		}
		isRecordingCalibration = true;
		displayText.text = "RECORDING LOUD...";
	}

	void UpdateGame ()
	{
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
		displayText.text = message + "\nPRIMARY: MENU";
		displayText.gameObject.SetActive(true);
		for (int i = 0; i < agents.Length; i++)
		{
			agents[i].EndGame();
		}

		for (int i = 0; i < cellObjects.Length; i++)
		{
			cellObjects[i].Recycle();
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
		maze.Dispose();
		scent.Dispose();
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
