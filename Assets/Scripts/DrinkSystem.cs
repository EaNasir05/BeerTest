using System.Collections;
using TMPro;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

enum DrinkState { Idle, Drinking, Returning }

public class DrinkSystem : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private GameObject handOnSteering;
    [SerializeField] private GameObject handOnGlass;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Liquid beer;
    [SerializeField] private float movementSpeedWhileReturning;
    [SerializeField] private float movementDurationBeforeDrinking;
    [SerializeField] private float returnDuration;
    [SerializeField] private float rotationSpeedWhileReturning;
    [SerializeField] private float drinkDuration;
    [SerializeField] private float shaderBugExtraFill;
    [SerializeField] private float minFill;
    [SerializeField] private float maxFill;
    [SerializeField] private float maxTilt;
    [SerializeField] private float maxHeight;
    [SerializeField] private float beerLossDuration;
    private InputActionMap inputMap;
    private InputAction holdN, holdS, holdE, holdW, trigger, collisionTest;
    private DrinkState state = DrinkState.Idle;
    private Vector3 startPos;
    private Quaternion startRot;
    private Coroutine routine;
    private float beerConsumed;
    private float totalBeerConsumed;
    private float extraFillWhileMoving;
    private float startingFill;
    private bool iHateNiggers;

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
        collisionTest = inputMap.FindAction("Jump");
        totalBeerConsumed = 0;
        startPos = transform.position;
        startRot = transform.rotation;
        iHateNiggers = false;
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
        if (collisionTest.WasPressedThisFrame())
        {
            StartCoroutine(GainBeer(0.2f));
        }
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
                iHateNiggers = true;
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

    private IEnumerator LoseBeer(float fillGain)
    {
        StartCoroutine(SimulateCarCollision());
        //spawna palline
        float elapsed = 0f;
        float previousFill = 0f;
        while (elapsed < beerLossDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / beerLossDuration);
            float currentFill = fillGain * t;
            float increment = currentFill - previousFill;
            if (beer.fillAmount + increment > maxFill)
            {
                increment = maxFill - beer.fillAmount;
            }
            beer.fillAmount += increment;
            previousFill = currentFill;
            yield return null;
        }
    }

    private IEnumerator SimulateCarCollision()
    {
        //alza e abbassa il calice e la camera
        yield return null;
    }

    private IEnumerator GainBeer(float fillLoss)
    {
        float elapsed = 0f;
        float previousFill = 0f;
        while (elapsed < beerLossDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / beerLossDuration);
            float currentFill = fillLoss * t;
            float decrement = currentFill - previousFill;
            if (beer.fillAmount - decrement < minFill)
            {
                decrement = minFill + beer.fillAmount;
            }
            beer.fillAmount -= decrement;
            previousFill = currentFill;
            yield return null;
        }
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
            if (elapsedMovement < movementDurationBeforeDrinking)
            {
                elapsedMovement += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedMovement / movementDurationBeforeDrinking);
                Vector3 absolutePos = Vector3.Lerp(startPos, targetPosition, t);
                Quaternion absoluteRot = Quaternion.Lerp(startRot, targetRotation, t);
                Vector3 deltaPos = absolutePos - transform.position;
                transform.position += deltaPos;
                Quaternion deltaRot = absoluteRot * Quaternion.Inverse(transform.rotation);
                transform.rotation = deltaRot * transform.rotation;
                float targetFill = Mathf.Lerp(startingFill, startingFill - shaderBugExtraFill, t);
                float deltaFill = beer.fillAmount - targetFill;
                beer.fillAmount -= deltaFill;
                extraFillWhileMoving = startingFill - beer.fillAmount;
            }
            else
            {
                elapsedDrinking += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedDrinking / realDrinkDuration);
                Quaternion absRot = Quaternion.Lerp(targetRotation, maxRotation, t);
                Vector3 absPos = Vector3.Lerp(targetPosition, maxPosition, t);
                Quaternion deltaRot = absRot * Quaternion.Inverse(transform.rotation);
                transform.rotation = deltaRot * transform.rotation;
                Vector3 deltaPos = absPos - transform.position;
                transform.position += deltaPos;

                float targetFill = Mathf.Lerp(startingFill, maxFill - shaderBugExtraFill, t);
                float deltaFill = beer.fillAmount - targetFill;
                beer.fillAmount -= deltaFill;

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
        float elapsed = 0f;
        float startFill = beer.fillAmount;
        float startExtra = extraFillWhileMoving;
        float baseFill = startFill + startExtra;
        float maxDistance = Vector3.Distance(new Vector3(targetTransform.position.x, maxHeight, targetTransform.position.z), startPos);
        float distance = Vector3.Distance(transform.position, startPos);
        float realReturnDuration = (distance / maxDistance) * returnDuration;

        Vector3 startPosAtReturn = transform.position;
        Quaternion startRotAtReturn = transform.rotation;

        while (elapsed < realReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnDuration);
            Vector3 absPos = Vector3.Lerp(startPosAtReturn, startPos, t);
            Quaternion absRot = Quaternion.Lerp(startRotAtReturn, startRot, t);
            Vector3 deltaPos = absPos - transform.position;
            transform.position += deltaPos;
            Quaternion deltaRot = absRot * Quaternion.Inverse(transform.rotation);
            transform.rotation = deltaRot * transform.rotation;
            float targetFill = Mathf.Lerp(baseFill, baseFill - startExtra, t);
            float deltaFill = beer.fillAmount - targetFill;
            beer.fillAmount -= deltaFill;
            extraFillWhileMoving = Mathf.Lerp(startExtra, 0f, t);
            yield return null;
        }
        if (iHateNiggers)
        {
            beer.fillAmount -= 1;
            iHateNiggers = false;
        }
        else
        {
            beer.fillAmount = baseFill;
        }

        extraFillWhileMoving = 0f;
        totalBeerConsumed += beerConsumed;
        state = DrinkState.Idle;

        Debug.Log("BEVUTO ORA: " + beerConsumed);
        Debug.Log("TOTALE BEVUTO: " + totalBeerConsumed);
    }

    public bool IsDrinking() => state == DrinkState.Drinking;
}
