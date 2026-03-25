using System;
using UnityEngine;
using UnityEngine.UI;

public class StartUI : MonoBehaviour
{
    [SerializeField] private InputField _playerIdInputField;
    [SerializeField] private InputField _speedInputField;
    [SerializeField] private Button _button;


    public void Login()
    {
        if (_playerIdInputField == null || _speedInputField == null)
        {
            Debug.LogError("_inputField is null");
            return;
        }

        string id = _playerIdInputField.text;
        int speed = Convert.ToInt32(_speedInputField.text);
        MasterManager.Instance.LocalPlayerId = id;
        NetworkManager.Instance.SendLoginRequest(id, speed);
    }
}