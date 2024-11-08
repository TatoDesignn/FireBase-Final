using Firebase.Auth;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AuthStateHandler : MonoBehaviour
{
    [SerializeField] private GameObject iniciarSesion;
    [SerializeField] private GameObject registro;
    [SerializeField] private GameObject inicio;

    void Start()
    {
        FirebaseAuth.DefaultInstance.StateChanged += HandleAuthStateChanged;
    }

    private void HandleAuthStateChanged(object sender, EventArgs e)
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            iniciarSesion.SetActive(false);
            registro.SetActive(false);
            inicio.SetActive(true);
        }
        else
        {
            registro.SetActive(true);
            iniciarSesion.SetActive(false);
            inicio.SetActive(false);

        }

    }
}