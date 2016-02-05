using UnityEngine;
using System.Collections;

public class ScoringUI : MonoBehaviour {
	public GameManager gameManager;

	void Awake()
	{
		gameManager.OnScoreChanged += GameManager_OnScoreChanged;
	}

	void GameManager_OnScoreChanged (int obj)
	{
		
	}
}
