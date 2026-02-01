using UnityEngine;

public class Speedometer : MonoBehaviour
{
    private CharacterController _cc;
    private float _speed;

    void Start() => _cc = GetComponent<CharacterController>();

    void OnGUI()
    {
        Vector3 hVel = new Vector3(_cc.velocity.x, 0, _cc.velocity.z);
        _speed = hVel.magnitude;

        // Quake measures units/sec. Unity meters * ~32 approximates Quake Units (UPS)
        float ups = _speed * 32f;

        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(Screen.width / 2 - 50, Screen.height - 100, 200, 100), Mathf.Round(ups).ToString(), style);
    }
}