using System.Collections;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.InputSystem;

public class DrinkSystem : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private GameObject handOnSteering;
    [SerializeField] private GameObject handOnGlass;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Liquid beer;
    [SerializeField] private float movementSpeed;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float drinkSpeed;
    [SerializeField] private float maxFill;
    [SerializeField] private float maxTilt;
    private InputActionMap inputMap;
    private InputAction holdNorth;
    private InputAction holdEast;
    private InputAction holdSouth;
    private InputAction holdWest;
    private InputAction rightTrigger;
    private bool holdingGlass;
    private bool returning;
    private bool drinking;
    Vector3 startPos;
    Quaternion startRot;
    Coroutine currentRoutine;

    private void Awake()
    {
        inputMap = inputActions.FindActionMap("Player");
        holdingGlass = false;
        drinking = false;
        returning = false;
        holdNorth = inputMap.FindAction("Hold N");
        holdSouth = inputMap.FindAction("Hold S");
        holdEast = inputMap.FindAction("Hold E");
        holdWest = inputMap.FindAction("Hold W");
        rightTrigger = inputMap.FindAction("Drink");
        startPos = transform.position;
        startRot = transform.rotation;
    }

    private void OnEnable()
    {
        inputMap.Enable();
    }

    private void OnDisable()
    {
        inputMap.Disable();
    }

    private void Update()
    {
        bool fourButtons = holdNorth.IsPressed() && holdSouth.IsPressed() && holdEast.IsPressed() && holdWest.IsPressed();
        bool trigger = rightTrigger.IsPressed();
        bool shouldStartDrink = fourButtons && trigger;

        if (shouldStartDrink && !drinking)
        {
            drinking = true;
            returning = false;
            RestartRoutine(Drink());
        }
        else if (!shouldStartDrink && drinking && !returning)
        {
            drinking = false;
            returning = true;
            RestartRoutine(ReturnToStart());
        }

        if (drinking || returning)
        {
            handOnSteering.SetActive(false);
            handOnGlass.SetActive(true);
        }
        else
        {
            if (fourButtons)
            {
                handOnGlass.SetActive(true);
                handOnSteering.SetActive(false);
            }
            else
            {
                handOnGlass.SetActive(false);
                handOnSteering.SetActive(true);
            }
        }

        if (drinking || (currentRoutine != null && !drinking))
        {
            beer.MaxWobble = 0.001f;
            beer.WobbleSpeedMove = 1f;
        }
        else
        {
            beer.MaxWobble = 0.05f;
            beer.WobbleSpeedMove = 1f;
        }
    }

    private IEnumerator Drink()
    {
        while (true)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetTransform.position,
                movementSpeed * Time.deltaTime);
            float normalizedFill = Mathf.Clamp01(beer.fillAmount / maxFill);
            float targetX = Mathf.Lerp(0f, maxTilt, normalizedFill);
            Quaternion dynamicTargetRot = Quaternion.Euler(targetX, targetTransform.rotation.eulerAngles.y, targetTransform.rotation.eulerAngles.z);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                dynamicTargetRot,
                rotationSpeed * Time.deltaTime
            );

            if (Quaternion.Angle(transform.rotation, dynamicTargetRot) < 1f)
            {
                beer.fillAmount += Time.deltaTime * drinkSpeed;
            }

            if (beer.fillAmount >= maxFill)
            {
                beer.fillAmount = maxFill;
                drinking = false;
                returning = true;
                RestartRoutine(ReturnToStart());
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator ReturnToStart()
    {
        while (true)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                startPos,
                movementSpeed * Time.deltaTime);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                startRot,
                rotationSpeed * Time.deltaTime);

            if (transform.position == startPos && transform.rotation == startRot)
            {
                returning = false;
                yield break;
            }

            yield return null;
        }
    }

    private void RestartRoutine(IEnumerator routine)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(routine);
    }

    public bool IsDrinking()
    {
        return drinking;
    }

    public bool IsHoldingTheGlass()
    {
        return holdingGlass;
    }
}
