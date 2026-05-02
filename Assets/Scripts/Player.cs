using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public enum MovementModality
{
	Controller,
	Voice
}

public enum VoiceCalibrationStep
{
	Quiet,
	Loud
}

[RequireComponent(typeof(XROrigin))]
public class Player : MonoBehaviour
{
	[SerializeField]
	XROrigin xrOrigin;

	[SerializeField, Min(0f)]
	float maxVoiceSpeed = 2.5f;

	[SerializeField, Min(0.1f)]
	float calibrationSeconds = 2f;

	[SerializeField, Min(0f)]
	float volumeSmoothing = 8f;

	[SerializeField, Range(0f, 1f)]
	float deadZone = 0.05f;

	[SerializeField, Min(0.0001f)]
	float minimumCalibrationRange = 0.01f;

	[SerializeField, Min(0f)]
	float voiceCollisionRadius = 0.25f;

	[SerializeField, Min(0f)]
	float voiceCollisionSkin = 0.03f;

	[SerializeField]
	LayerMask voiceCollisionMask = ~0;

	Transform head;

	CharacterController characterController;

	ContinuousMoveProvider controllerMoveProvider;

	MovementModality movementModality = MovementModality.Controller;

	AudioClip microphoneClip;

	string microphoneDevice;

	float[] microphoneSamples;

	float quietVolume;

	float loudVolume = 0.1f;

	float calibrationTimer;

	float calibrationTotal;

	int calibrationSampleCount;

	bool isCalibratingVoice;

	VoiceCalibrationStep calibrationStep;

	bool voiceMovementEnabled;

	float currentVoiceLevel;

	float lastMicrophoneVolume;

	RaycastHit[] voiceCollisionHits = new RaycastHit[8];

	public float LastMicrophoneVolume => lastMicrophoneVolume;

	public float QuietVolume => quietVolume;

	public float LoudVolume => loudVolume;

	void Awake ()
	{
		CacheReferences();
		SetMovementModality(MovementModality.Controller);
	}

	void Update ()
	{
		if (movementModality == MovementModality.Voice && voiceMovementEnabled)
		{
			ApplyVoiceMovement();
		}
	}

	void CacheReferences ()
	{
		if (xrOrigin == null)
		{
			xrOrigin = GetComponent<XROrigin>();
		}

		if (xrOrigin != null && xrOrigin.Camera != null)
		{
			head = xrOrigin.Camera.transform;
		}

		if (characterController == null)
		{
			characterController = GetComponent<CharacterController>();
		}

		if (controllerMoveProvider == null)
		{
			controllerMoveProvider = GetComponentInChildren<ContinuousMoveProvider>(true);
		}
	}

	public void SetMovementModality (MovementModality modality)
	{
		CacheReferences();
		movementModality = modality;
		voiceMovementEnabled = false;
		currentVoiceLevel = 0f;

		if (controllerMoveProvider != null)
		{
			controllerMoveProvider.enabled = modality == MovementModality.Controller;
		}

		if (modality == MovementModality.Controller)
		{
			StopMicrophone();
		}
	}

	public bool BeginVoiceCalibration (VoiceCalibrationStep step)
	{
		if (!EnsureMicrophone())
		{
			return false;
		}

		calibrationStep = step;
		calibrationTimer = 0f;
		calibrationTotal = 0f;
		calibrationSampleCount = 0;
		isCalibratingVoice = true;
		return true;
	}

	public bool UpdateVoiceCalibration ()
	{
		if (!isCalibratingVoice)
		{
			return true;
		}

		calibrationTimer += Time.deltaTime;
		calibrationTotal += SampleMicrophoneRms();
		calibrationSampleCount += 1;

		if (calibrationTimer < calibrationSeconds)
		{
			return false;
		}

		float averageVolume = calibrationSampleCount > 0 ?
			calibrationTotal / calibrationSampleCount : 0f;
		if (calibrationStep == VoiceCalibrationStep.Quiet)
		{
			quietVolume = averageVolume;
		}
		else
		{
			loudVolume = averageVolume;
		}
		isCalibratingVoice = false;
		return true;
	}

	public bool HasUsableVoiceCalibration () =>
		loudVolume - quietVolume >= minimumCalibrationRange;

	public void UseFallbackVoiceCalibration ()
	{
		loudVolume = quietVolume + minimumCalibrationRange;
	}

	public void EndGame ()
	{
		voiceMovementEnabled = false;
		currentVoiceLevel = 0f;
		StopMicrophone();
	}

	public void StartNewGame (Vector3 position)
	{
		CacheReferences();

		float yaw = Random.Range(0f, 360f);
		transform.rotation = Quaternion.Euler(0f, yaw, 0f);

		// Move only in X/Z so we do not fight headset height tracking.
		if (head != null)
		{
			Vector3 delta = new Vector3(
				position.x - head.position.x,
				0f,
				position.z - head.position.z
			);
			transform.position += delta;
		}
		else
		{
			transform.position = new Vector3(
				position.x,
				transform.position.y,
				position.z
			);
		}

		voiceMovementEnabled =
			movementModality == MovementModality.Voice && EnsureMicrophone();
	}

	public Vector3 Move ()
	{
		CacheReferences();

		Transform t = head != null ? head : transform;

		// Maze logic only cares about X/Z.
		return new Vector3(t.position.x, 0f, t.position.z);
	}

	void ApplyVoiceMovement ()
	{
		Transform forwardTransform = head != null ? head : transform;
		Vector3 direction = Vector3.ProjectOnPlane(
			forwardTransform.forward, Vector3.up
		).normalized;
		if (direction.sqrMagnitude <= Mathf.Epsilon)
		{
			return;
		}

		float volumeRange = Mathf.Max(loudVolume - quietVolume, minimumCalibrationRange);
		float targetLevel = Mathf.Clamp01(
			(SampleMicrophoneRms() - quietVolume) / volumeRange
		);
		if (targetLevel < deadZone)
		{
			targetLevel = 0f;
		}
		else
		{
			targetLevel = Mathf.InverseLerp(deadZone, 1f, targetLevel);
		}
		currentVoiceLevel = Mathf.Lerp(
			currentVoiceLevel,
			targetLevel,
			1f - Mathf.Exp(-volumeSmoothing * Time.deltaTime)
		);

		Vector3 movement = direction * (currentVoiceLevel * maxVoiceSpeed * Time.deltaTime);
		movement = ConstrainVoiceMovement(movement);
		if (movement.sqrMagnitude <= Mathf.Epsilon)
		{
			return;
		}

		if (characterController != null && characterController.enabled)
		{
			characterController.Move(movement);
		}
		else
		{
			transform.position += movement;
		}
	}

	Vector3 ConstrainVoiceMovement (Vector3 movement)
	{
		float distance = movement.magnitude;
		if (distance <= Mathf.Epsilon)
		{
			return Vector3.zero;
		}

		Vector3 direction = movement / distance;
		Vector3 origin = GetVoiceCollisionOrigin();
		int hitCount = Physics.SphereCastNonAlloc(
			origin,
			voiceCollisionRadius,
			direction,
			voiceCollisionHits,
			distance + voiceCollisionSkin,
			voiceCollisionMask,
			QueryTriggerInteraction.Ignore
		);

		float allowedDistance = distance;
		for (int i = 0; i < hitCount; i++)
		{
			Collider hitCollider = voiceCollisionHits[i].collider;
			if (hitCollider == null)
			{
				continue;
			}

			Transform hitTransform = hitCollider.transform;
			if (hitTransform == transform || hitTransform.IsChildOf(transform))
			{
				continue;
			}

			if (voiceCollisionHits[i].distance <= voiceCollisionSkin)
			{
				return Vector3.zero;
			}
			allowedDistance = Mathf.Min(
				allowedDistance,
				Mathf.Max(0f, voiceCollisionHits[i].distance - voiceCollisionSkin)
			);
		}

		return direction * allowedDistance;
	}

	Vector3 GetVoiceCollisionOrigin ()
	{
		Vector3 playerPosition = head != null ? head.position : transform.position;
		float collisionHeight = playerPosition.y;
		if (characterController != null)
		{
			collisionHeight = transform.TransformPoint(characterController.center).y;
		}
		return new Vector3(playerPosition.x, collisionHeight, playerPosition.z);
	}

	bool EnsureMicrophone ()
	{
		if (microphoneClip != null)
		{
			return true;
		}

#if UNITY_ANDROID && !UNITY_EDITOR
		if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
			UnityEngine.Android.Permission.Microphone
		))
		{
			UnityEngine.Android.Permission.RequestUserPermission(
				UnityEngine.Android.Permission.Microphone
			);
			return false;
		}
#endif

		if (Microphone.devices.Length == 0)
		{
			return false;
		}

		microphoneDevice = Microphone.devices[0];
		int sampleRate = AudioSettings.outputSampleRate;
		microphoneClip = Microphone.Start(microphoneDevice, true, 1, sampleRate);
		if (microphoneSamples == null || microphoneSamples.Length != 256)
		{
			microphoneSamples = new float[256];
		}
		return microphoneClip != null;
	}

	void StopMicrophone ()
	{
		isCalibratingVoice = false;
		if (microphoneClip == null)
		{
			return;
		}

		Microphone.End(microphoneDevice);
		microphoneClip = null;
		microphoneDevice = null;
	}

	public float PreviewMicrophoneVolume ()
	{
		if (!EnsureMicrophone())
		{
			lastMicrophoneVolume = 0f;
			return lastMicrophoneVolume;
		}
		return SampleMicrophoneRms();
	}

	float SampleMicrophoneRms ()
	{
		if (microphoneClip == null || microphoneSamples == null)
		{
			lastMicrophoneVolume = 0f;
			return 0f;
		}

		int microphonePosition = Microphone.GetPosition(microphoneDevice);
		if (microphonePosition <= 0)
		{
			lastMicrophoneVolume = 0f;
			return 0f;
		}

		int startPosition = microphonePosition - microphoneSamples.Length;
		if (startPosition < 0)
		{
			startPosition += microphoneClip.samples;
		}

		microphoneClip.GetData(microphoneSamples, startPosition);

		float sum = 0f;
		for (int i = 0; i < microphoneSamples.Length; i++)
		{
			sum += microphoneSamples[i] * microphoneSamples[i];
		}
		lastMicrophoneVolume = Mathf.Sqrt(sum / microphoneSamples.Length);
		return lastMicrophoneVolume;
	}

	void OnDestroy ()
	{
		StopMicrophone();
	}
}
