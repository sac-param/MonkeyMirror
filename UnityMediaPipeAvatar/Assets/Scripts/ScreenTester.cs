using System.Collections;
using UnityEngine;

public class ScreenTester : MonoBehaviour
{
    [Header("All Screens — assign all here")]
    public GameObject defaultScreen;
    public GameObject waveScreen;
    public GameObject danceScreen;
    public GameObject jumpScreen;
    public GameObject poseScreen;
    public GameObject thankYouScreen;
    public GameObject getReadyScreen;

    [Header("Settings")]
    public float secondsPerScreen = 5f;

    private GameObject[] _screens;
    private int _current = 0;

    private void Start()
    {
        _screens = new GameObject[]
        {
            defaultScreen,
            waveScreen,
            danceScreen,
            jumpScreen,
            poseScreen,
            thankYouScreen,
            getReadyScreen
        };

        StartCoroutine(LoopScreens());
    }

    private IEnumerator LoopScreens()
    {
        while (true)
        {
            // Hide all
            foreach (var s in _screens)
                if (s != null) s.SetActive(false);

            // Show current
            if (_screens[_current] != null)
            {
                _screens[_current].SetActive(true);
                Debug.Log($"[ScreenTester] Showing: {_screens[_current].name}");
            }

            yield return new WaitForSeconds(secondsPerScreen);

            // Next screen
            _current = (_current + 1) % _screens.Length;
        }
    }
}