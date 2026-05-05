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
	float voiceCollisionRadius = 0.12f;

	[SerializeField, Min(0f)]
	float voiceCollisionSkin = 0.01f;

	[SerializeField]
	LayerMask voiceCollisionMask = ~0;

	[SerializeField, Min(8000)]
	int microphoneSampleRate = 48000;

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

	bool isMovementPaused;

	float currentVoiceLevel;

	float lastMicrophoneVolume;

	int lastMicrophonePosition;

	int lastMicrophoneDeviceCount;

	string microphoneStatus = "MIC OFF";

	RaycastHit[] voiceCollisionHits = new RaycastHit[8];

	public float LastMicrophoneVolume => lastMicrophoneVolume;

	public float QuietVolume => quietVolume;

	public float LoudVolume => loudVolume;

	public string MicrophoneStatus => microphoneStatus;

	public Transform ViewTransform
	{
		get
		{
			CacheReferences();
			return head != null ? head : transform;
		}
	}

	void Awake ()
	{
		CacheReferences();
		SetMovementModality(MovementModality.Controller);
	}

	void Update ()
	{
		if (
			movementModality == MovementModality.Voice &&
			voiceMovementEnabled &&
			!isMovementPaused
		)
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
			controllerMoveProvider.enabled =
				modality == MovementModality.Controller && !isMovementPaused;
		}

		if (modality == MovementModality.Controller)
		{
			StopMicrophone();
		}
	}

	public void SetMovementPaused (bool paused)
	{
		CacheReferences();
		isMovementPaused = paused;
		currentVoiceLevel = 0f;

		if (controllerMoveProvider != null)
		{
			controllerMoveProvider.enabled =
				movementModality == MovementModality.Controller && !isMovementPaused;
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
		isMovementPaused = false;
		voiceMovementEnabled = false;
		currentVoiceLevel = 0f;
		StopMicrophone();
	}

	public void StartNewGame (Vector3 position)
	{
		CacheReferences();
		SetMovementPaused(false);

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
		if (!TryGetNearestVoiceCollision(origin, direction, distance, out RaycastHit hit))
		{
			return movement;
		}

		float allowedDistance = Mathf.Max(0f, hit.distance - voiceCollisionSkin);
		Vector3 forwardMovement = direction * Mathf.Min(distance, allowedDistance);
		float remainingDistance = distance - forwardMovement.magnitude;
		if (remainingDistance <= Mathf.Epsilon)
		{
			return forwardMovement;
		}

		Vector3 slideDirection = Vector3.ProjectOnPlane(direction, hit.normal);
		slideDirection.y = 0f;
		if (slideDirection.sqrMagnitude <= Mathf.Epsilon)
		{
			return forwardMovement;
		}

		slideDirection.Normalize();
		if (TryGetNearestVoiceCollision(
			origin + forwardMovement,
			slideDirection,
			remainingDistance,
			out RaycastHit slideHit
		))
		{
			remainingDistance = Mathf.Max(0f, slideHit.distance - voiceCollisionSkin);
		}

		return forwardMovement + slideDirection * remainingDistance;
	}

	bool TryGetNearestVoiceCollision (
		Vector3 origin, Vector3 direction, float distance, out RaycastHit nearestHit
	)
	{
		nearestHit = default(RaycastHit);
		int hitCount = Physics.SphereCastNonAlloc(
			origin,
			voiceCollisionRadius,
			direction,
			voiceCollisionHits,
			distance + voiceCollisionSkin,
			voiceCollisionMask,
			QueryTriggerInteraction.Ignore
		);

		float nearestDistance = float.MaxValue;
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

			if (voiceCollisionHits[i].distance < nearestDistance)
			{
				nearestDistance = voiceCollisionHits[i].distance;
				nearestHit = voiceCollisionHits[i];
			}
		}
		return nearestDistance < float.MaxValue;
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
			UpdateMicrophoneStatus();
			return true;
		}

#if UNITY_ANDROID && !UNITY_EDITOR
		if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
			UnityEngine.Android.Permission.Microphone
		))
		{
			microphoneStatus = "MIC PERMISSION REQUESTED";
			UnityEngine.Android.Permission.RequestUserPermission(
				UnityEngine.Android.Permission.Microphone
			);
			return false;
		}
#endif

		lastMicrophoneDeviceCount = Microphone.devices.Length;
#if UNITY_ANDROID && !UNITY_EDITOR
		microphoneDevice = null;
#else
		if (lastMicrophoneDeviceCount == 0)
		{
			microphoneStatus = "NO MICROPHONE DEVICE";
			return false;
		}
		microphoneDevice = Microphone.devices[0];
#endif
		microphoneClip = Microphone.Start(
			microphoneDevice, true, 2, microphoneSampleRate
		);
		if (microphoneSamples == null || microphoneSamples.Length != 256)
		{
			microphoneSamples = new float[256];
		}
		if (microphoneClip == null)
		{
			microphoneStatus = "MIC START FAILED";
			return false;
		}

		UpdateMicrophoneStatus();
		return true;
	}

	void StopMicrophone ()
	{
		isCalibratingVoice = false;
		if (microphoneClip == null)
		{
			microphoneStatus = "MIC OFF";
			return;
		}

		Microphone.End(microphoneDevice);
		microphoneClip = null;
		microphoneDevice = null;
		lastMicrophonePosition = 0;
		microphoneStatus = "MIC OFF";
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
		lastMicrophonePosition = microphonePosition;
		if (microphonePosition <= 0)
		{
			lastMicrophoneVolume = 0f;
			UpdateMicrophoneStatus();
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
		UpdateMicrophoneStatus();
		return lastMicrophoneVolume;
	}

	void UpdateMicrophoneStatus ()
	{
		lastMicrophoneDeviceCount = Microphone.devices.Length;
		string deviceName = string.IsNullOrEmpty(microphoneDevice) ?
			"DEFAULT" : microphoneDevice;
		microphoneStatus =
			"MIC " + deviceName +
			" DEVICES " + lastMicrophoneDeviceCount +
			" POS " + lastMicrophonePosition +
			" RATE " + microphoneSampleRate;
	}

	void OnDestroy ()
	{
		StopMicrophone();
	}
}
