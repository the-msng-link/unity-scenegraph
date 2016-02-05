using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour {
	public PlayerController player;
	public ScoringUI scoringUI;

	public event System.Action<int> OnScoreChanged;
}
