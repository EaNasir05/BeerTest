using System.Collections;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.InputSystem;

enum DrinkState { Idle, Drinking, Returning }

public class DrinkSystem : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private GameObject handOnSteering;
    [SerializeField] private GameObject handOnGlass;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Liquid beer;
    [SerializeField] private float movementSpeed;
    [SerializeField] private float movementDuration;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float drinkSpeed;
    [SerializeField] private float maxFill;
    [SerializeField] private float maxTilt;
    private InputActionMap inputMap;
    private InputAction holdN, holdS, holdE, holdW, trigger;
    private DrinkState state = DrinkState.Idle;
    private Vector3 startPos;
    private Quaternion startRot;
    private Coroutine routine;
    private float beerConsumed;
    private float totalBeerConsumed;
    private float extraFillWhileMoving;
    private float prevLogicalVolume;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        inputMap = inputActions.FindActionMap("Player");
        holdN = inputMap.FindAction("Hold N");
        holdS = inputMap.FindAction("Hold S");
        holdE = inputMap.FindAction("Hold E");
        holdW = inputMap.FindAction("Hold W");
        trigger = inputMap.FindAction("Drink");
        totalBeerConsumed = 0;
        startPos = transform.position;
        startRot = transform.rotation;
    }

    private void OnEnable() => inputMap.Enable();
    private void OnDisable() => inputMap.Disable();

    private void Update()
    {
        bool fourButtons = holdN.IsPressed() && holdS.IsPressed() && holdE.IsPressed() && holdW.IsPressed();
        bool drinkBtn = trigger.IsPressed();
        bool shouldDrink = fourButtons && drinkBtn;
        switch (state)
        {
            case DrinkState.Idle:
                if (shouldDrink)
                    StartDrinking();
                break;
            case DrinkState.Drinking:
                if (!shouldDrink)
                    StartReturning();
                break;
            case DrinkState.Returning:
                break;
        }
        UpdateHands(fourButtons);
        UpdateWobble();
    }

    private void UpdateHands(bool fourButtons)
    {
        bool handShouldHold = state == DrinkState.Drinking || state == DrinkState.Returning || (state == DrinkState.Idle && fourButtons);
        handOnGlass.SetActive(handShouldHold);
        handOnSteering.SetActive(!handShouldHold);
    }

    private void UpdateWobble()
    {
        bool stable = state == DrinkState.Drinking || state == DrinkState.Returning;
        beer.MaxWobble = stable ? 0.001f : 0.05f;
    }

    private void StartDrinking()
    {
        if (beer.fillAmount < maxFill)
        {
            state = DrinkState.Drinking;
            RestartRoutine(DrinkRoutine());
        }
    }

    private void StartReturning()
    {
        if (state == DrinkState.Drinking || state == DrinkState.Idle) //NON MI CONVINCE
        {
            state = DrinkState.Returning;
            RestartRoutine(ReturnRoutine());
        }
    }

    private void RestartRoutine(IEnumerator newRoutine)
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(newRoutine);
    }

    private IEnumerator DrinkRoutine()
    {
        float x = (beer.fillAmount - 0.15f) * -0.0131818181818182f;
        if (x < -45)
            x = -45;
        Quaternion targetRotation = new Quaternion(x, transform.rotation.y, transform.rotation.z, transform.rotation.w); //NON MI CONVINCE
        beerConsumed = 0f;
        extraFillWhileMoving = 0f;
        float startFill = beer.fillAmount;
        prevLogicalVolume = maxFill - beer.fillAmount;
        float totalDistance = Vector3.Distance(startPos, targetTransform.position);
        float elapsedMovement = 0f;
        float elapsedDrink = 0f;

        while (state == DrinkState.Drinking)
        {
            if (elapsedMovement < movementDuration)
            {
                elapsedMovement += Time.deltaTime;
                float t = elapsedMovement / movementDuration;
                transform.rotation = Quaternion.Lerp(startRot, targetRotation, t);
                transform.position = Vector3.Lerp(startPos, targetTransform.position, t);
                beer.fillAmount = Mathf.Lerp(startFill, startFill - 0.45f, t);
                extraFillWhileMoving = startFill - beer.fillAmount;
            }
            else //SONO RIMASTO QUI
            {
                float normalizedFill = Mathf.Clamp01(beer.fillAmount / maxFill);
                float tiltX = Mathf.Lerp(0f, maxTilt, normalizedFill);
                Quaternion desiredRot = Quaternion.Euler(
                    tiltX,
                    targetTransform.rotation.eulerAngles.y,
                    targetTransform.rotation.eulerAngles.z
                );
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    desiredRot,
                    rotationSpeed * Time.deltaTime
                );

                if (Quaternion.Angle(transform.rotation, desiredRot) < 1f)
                {
                    float deltaFill = Time.deltaTime * drinkSpeed;
                    beer.fillAmount = Mathf.Clamp(beer.fillAmount + deltaFill, 0f, maxFill);

                    float newLogicalVolume = maxFill - beer.fillAmount;
                    float deltaConsumed = prevLogicalVolume - newLogicalVolume;
                    if (deltaConsumed > 0f)
                    {
                        beerConsumed += deltaConsumed;
                        prevLogicalVolume = newLogicalVolume;
                    }
                }

                if (beer.fillAmount >= maxFill - 0.45)
                {
                    StartReturning();
                    yield break;
                }
            }
            yield return null;
        }
    }

    private IEnumerator ReturnRoutine()
    {
        float startFill = beer.fillAmount;
        float startExtra = extraFillWhileMoving;
        float baseFill = startFill + startExtra;

        Vector3 returnStartPos = transform.position;
        float totalDistance = Vector3.Distance(returnStartPos, startPos);

        while (state == DrinkState.Returning)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                startPos,
                movementSpeed * Time.deltaTime
            );

            float currentDistance = Vector3.Distance(transform.position, returnStartPos);
            float t = Mathf.Clamp01(currentDistance / totalDistance);

            float currentExtra = Mathf.Lerp(startExtra, 0f, t);
            beer.fillAmount = Mathf.Clamp(baseFill + currentExtra, 0f, maxFill);
            extraFillWhileMoving = currentExtra;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                startRot,
                rotationSpeed * Time.deltaTime
            );

            if (transform.position == startPos && transform.rotation == startRot)
            {
                beer.fillAmount = baseFill;
                if (beer.fillAmount > maxFill)
                {
                    beer.fillAmount = maxFill;
                }
                extraFillWhileMoving = 0f;
                totalBeerConsumed += beerConsumed;
                state = DrinkState.Idle;

                Debug.Log("BEVUTO ORA: " + beerConsumed);
                Debug.Log("TOTALE BEVUTO: " + totalBeerConsumed);
                yield break;
            }

            yield return null;
        }
    }

    public bool IsDrinking() => state == DrinkState.Drinking;
}
