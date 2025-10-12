using UnityEngine;
using UnityEngine.InputSystem;
using System;


public class InputHandler : MonoBehaviour
{
	[Header("Character Input Values")]
	public Vector2 move, look;
	public bool jump, sprint, crouch, attack, aim, drop, log;
	public bool equip1, equip2, equip3;
	public bool toggleEquip;
	public float cycleInput;

	public event Action OnInteractPerformed;

	[Header("Movement Settings")]
	public bool analogMovement;

	[Header("Mouse Cursor Settings")]
	public bool cursorLocked = true;
	public bool cursorInputForLook = true;

	//STANDARD MOVEMENT

	void Start()
	{
		SetCursorState(true);
	}

	public void OnMove(InputValue value)
	{
		MoveInput(value.Get<Vector2>());
	}

	public void OnLook(InputValue value)
	{
		if (cursorInputForLook)
		{
			LookInput(value.Get<Vector2>());
		}
	}

	//SPECIAL MOVEMENT

	public void OnJump(InputValue value)
	{
		JumpInput(value.isPressed);
	}

	public void OnSprint(InputValue value)
	{
		SprintInput(value.isPressed);
	}

	public void OnCrouch(InputValue value)
	{
		CrouchInput(value.isPressed);
	}

	//ATTACK

	public void OnAttack(InputValue value)
	{
		AttackInput(value.isPressed);
	}

	public void OnAim(InputValue value)
	{
		AimInput(value.isPressed);
	}

	public void OnDrop(InputValue value)
	{
		DropInput(value.isPressed);
	}

	public void OnLog(InputValue value)
	{
		LogInput(value.isPressed);
	}


	// INTERACT

	public void OnInteract(InputValue value)
	{
		if (value.isPressed)
			OnInteractPerformed?.Invoke(); // fires once per press
	}

	public void OnCycle(InputValue value)
	{
		Vector2 scrollValue = value.Get<Vector2>();
		cycleInput = scrollValue.y;

		if (cycleInput > 0)
		{
			CycleForward();
		}
		else if (cycleInput < 0)
		{
			CycleBackward();
		}
	}

	public void CycleForward()
	{
		if (equip1)
		{
			equip1 = false;
			equip2 = true;
			equip3 = false;
		}
		else if (equip2)
		{
			equip2 = false;
			equip1 = true;
			equip3 = false;
		}
		else
		{
			equip1 = true;
			equip3 = false;
		}
	}

	public void CycleBackward()
	{
		CycleForward();
	}

	public void OnToggleEquip(InputValue value)
	{
		ToggleEquipInput(value.isPressed);

		if (value.isPressed)
		{
			CycleForward();
		}
	}

	//EQUIP

	public void OnEquip1(InputValue value)
	{
		EquipInput(1, value.isPressed);
	}

	public void OnEquip2(InputValue value)
	{
		EquipInput(2, value.isPressed);
	}

	public void OnEquip3(InputValue value)
	{
		equip1 = false;
		equip2 = false;
		EquipInput(3, value.isPressed);
	}

	//INPUT

	public void MoveInput(Vector2 newMoveDirection)
	{
		move = newMoveDirection;
	}

	public void LookInput(Vector2 newLookDirection)
	{
		look = newLookDirection;
	}

	public void JumpInput(bool newJumpState)
	{
		jump = newJumpState;
	}

	public void SprintInput(bool newSprintState)
	{
		sprint = newSprintState;
	}

	public void CrouchInput(bool newCrouchState)
	{
		crouch = newCrouchState;
	}

	public void AttackInput(bool newAttackState)
	{
		attack = newAttackState;
	}

	public void AimInput(bool newAimState)
	{
		aim = newAimState;
	}

	public void DropInput(bool newDropState)
	{
		drop = newDropState;
	}

	public void LogInput(bool newLogState)
	{
		log = newLogState;
	}

	public void CycleInput(float newCycleState)
	{
		cycleInput = newCycleState;
	}

	public void ToggleEquipInput(bool newToggleEquipState)
	{
		toggleEquip = newToggleEquipState;
	}

	public void EquipInput(int equipSlot, bool newState)
	{
		switch (equipSlot)
		{
			case 1:
				equip1 = newState;
				break;
			case 2:
				equip2 = newState;
				break;
			case 3:
				equip3 = newState;
				break;
			default:
				Debug.LogError("Invalid equipment slot");
				break;
		}
	}

	private void OnApplicationFocus(bool hasFocus)
	{
		SetCursorState(cursorLocked);
	}

	public void SetCursorState(bool newState)
	{
		Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
	}
}
