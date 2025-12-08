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
    [SerializeField] private float movementSpeedWhileDrinking;
    [SerializeField] private float movementSpeedWhileReturning;
    [SerializeField] private float movementDuration;
    [SerializeField] private float rotationSpeedWhileDrinking;
    [SerializeField] private float rotationSpeedWhileReturning;
    [SerializeField] private float drinkDuration;
    [SerializeField] private float shaderBugExtraFill;
    [SerializeField] private float minFill;
    [SerializeField] private float maxFill;
    [SerializeField] private float maxTilt;
    [SerializeField] private float maxHeight;
    private InputActionMap inputMap;
    private InputAction holdN, holdS, holdE, holdW, trigger;
    private DrinkState state = DrinkState.Idle;
    private Vector3 startPos;
    private Quaternion startRot;
    private Coroutine routine;
    private float beerConsumed;
    private float totalBeerConsumed;
    private float extraFillWhileMoving;
    private float startingFill;

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
                {
                    beerConsumed = beer.fillAmount + extraFillWhileMoving - startingFill;
                    StartReturning();
                }
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
        beer.MaxWobble = stable ? 0.01f : 0.05f;
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
        if (state == DrinkState.Drinking || state == DrinkState.Idle)
        {
            if (beer.fillAmount + extraFillWhileMoving >= maxFill)
            {
                beer.fillAmount = maxFill + 1;
            }
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
        float tRot = Mathf.InverseLerp(minFill, maxFill, beer.fillAmount);
        float xRot = Mathf.Lerp(-5, maxTilt, tRot);
        float yPos = Mathf.Lerp(targetTransform.position.y, maxHeight, tRot);
        Quaternion targetRotation = Quaternion.Euler(xRot, transform.rotation.eulerAngles.y, transform.rotation.eulerAngles.z);
        Vector3 targetPosition = new Vector3(targetTransform.position.x, yPos, targetTransform.position.z);
        Quaternion maxRotation = Quaternion.Euler(maxTilt, transform.rotation.eulerAngles.y, transform.rotation.eulerAngles.z);
        Vector3 maxPosition = new Vector3(targetTransform.position.x, maxHeight, targetTransform.position.z);
        beerConsumed = 0f;
        extraFillWhileMoving = 0f;
        startingFill = beer.fillAmount;
        float totalDistance = Vector3.Distance(startPos, targetTransform.position);
        float elapsedMovement = 0f;
        float elapsedDrinking = 0f;
        float realDrinkDuration = (beer.fillAmount - maxFill) * -1 * drinkDuration;
        while (state == DrinkState.Drinking)
        {
            if (elapsedMovement < movementDuration)
            {
                elapsedMovement += Time.deltaTime;
                float t = elapsedMovement / movementDuration;
                transform.rotation = Quaternion.Lerp(startRot, targetRotation, t);
                transform.position = Vector3.Lerp(startPos, targetPosition, t);
                beer.fillAmount = Mathf.Lerp(startingFill, startingFill - shaderBugExtraFill, t);
                extraFillWhileMoving = startingFill - beer.fillAmount;
            }
            else
            {
                elapsedDrinking += Time.deltaTime;
                float t = elapsedDrinking / realDrinkDuration;
                transform.rotation = Quaternion.Lerp(targetRotation, maxRotation, t);
                transform.position = Vector3.Lerp(targetPosition, maxPosition, t);
                beer.fillAmount = Mathf.Lerp(startingFill, maxFill - shaderBugExtraFill, t);

                if (beer.fillAmount >= maxFill - shaderBugExtraFill)
                {
                    beerConsumed = beer.fillAmount + extraFillWhileMoving - startingFill;
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
                movementSpeedWhileReturning * Time.deltaTime
            );

            float currentDistance = Vector3.Distance(transform.position, returnStartPos);
            float t = Mathf.Clamp01(currentDistance / totalDistance);

            if (baseFill < maxFill)
            {
                float currentExtra = Mathf.Lerp(startExtra, 0f, t);
                beer.fillAmount = Mathf.Clamp(baseFill + currentExtra, 0f, maxFill);
                extraFillWhileMoving = currentExtra;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                startRot,
                rotationSpeedWhileReturning * Time.deltaTime
            );

            if (transform.position == startPos && transform.rotation == startRot)
            {
                beer.fillAmount = baseFill;
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
