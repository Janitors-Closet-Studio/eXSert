using System.Collections;
using UnityEngine;

// [RequireComponent(typeof(MeshRenderer))]
public class ProjectileTurretEnemy : BaseTurretEnemy
{
	protected override bool ShouldHideTelegraphBeforeShot()
	{
		return false;
	}

	protected override bool ShouldUseSolidTelegraphBeforeShot()
	{
		return true;
	}
}