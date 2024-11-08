using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ControllerManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField[] inputsData;
    [SerializeField] private TMP_InputField inputScore;
    [SerializeField] private TMP_InputField inputUsername;
    [SerializeField] private TextMeshProUGUI[] textos;
    [SerializeField] private GameObject listaUsuarios;
    [SerializeField] private GameObject usuarioPrefab;
    [SerializeField] private Transform contenedorUsuarios;

    [SerializeField] private GameObject objetoActivar;  
    [SerializeField] private GameObject objetoPartida;  
    [SerializeField] private GameObject objetoinvitar;  
    [SerializeField] private GameObject objetoInicio; 
    [SerializeField] private TextMeshProUGUI emisor;    
    [SerializeField] private Button botonAceptar;       

    [SerializeField] private TextMeshProUGUI textoJugadorPropio; 
    [SerializeField] private TextMeshProUGUI textoJugadorOtro;   

    private DatabaseReference mDataBaseRef;
    private string currentUserId;
    private string emisorId; 
    private string emisorNombre; 

    private void Start()
    {
        mDataBaseRef = FirebaseDatabase.DefaultInstance.RootReference;
        currentUserId = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;

        
        FirebaseDatabase.DefaultInstance.GetReference("online-users").ChildAdded += HandleUserConnected;
        FirebaseDatabase.DefaultInstance.GetReference("online-users").ChildRemoved += HandleUserDisconnected;

        
        if (currentUserId != null)
        {
            FirebaseDatabase.DefaultInstance.GetReference($"invitaciones/{currentUserId}")
                .ValueChanged += HandleInvitationReceived;

            
            FirebaseDatabase.DefaultInstance.GetReference($"partidas/{currentUserId}")
                .ValueChanged += HandlePartidaAceptada;
        }

        ActualizarDatos();
    }

    public void ObtenerDatos(int funcion)
    {
        if (funcion < 3)
        {
            if (funcion == 1)
            {
                StartCoroutine(HandleRegisternButton());
            }
            else if (funcion == 2)
            {
                HandleLoginButton();
            }
        }
        else if (funcion == 4)
        {
            listaUsuarios.SetActive(!listaUsuarios.activeInHierarchy);
            if (listaUsuarios.activeInHierarchy)
            {
                ListUsers();
            }
        }
        else if (funcion == 5)
        {
            HandleLogOutButton();
        }
    }

    private void HandleLoginButton()
    {
        string email = inputsData[0].text;
        string password = inputsData[1].text;

        var auth = FirebaseAuth.DefaultInstance;

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Error al iniciar sesión: " + task.Exception);
                return;
            }

            Firebase.Auth.AuthResult result = task.Result;
            Debug.Log($"User signed in successfully: {result.User.Email} ({result.User.UserId})");
            currentUserId = result.User.UserId;
            AddUserToOnline(currentUserId);

            if (currentUserId != null)
            {
                FirebaseDatabase.DefaultInstance.GetReference($"invitaciones/{currentUserId}")
                    .ValueChanged += HandleInvitationReceived;

                FirebaseDatabase.DefaultInstance.GetReference($"partidas/{currentUserId}")
                    .ValueChanged += HandlePartidaAceptada;
            }

            ActualizarDatos();
        });
    }

    private IEnumerator HandleRegisternButton()
    {
        string email = inputsData[2].text;
        string password = inputsData[3].text;
        string username = inputUsername.text;

        var auth = FirebaseAuth.DefaultInstance;
        var registerTask = auth.CreateUserWithEmailAndPasswordAsync(email, password);

        yield return new WaitUntil(() => registerTask.IsCompleted);

        if (registerTask.IsCanceled || registerTask.IsFaulted)
        {
            Debug.LogError("Error al registrar: " + registerTask.Exception);
        }
        else
        {
            var result = registerTask.Result;
            Debug.Log($"Firebase user created successfully: {result.User.Email} ({result.User.UserId})");

            mDataBaseRef.Child("users").Child(result.User.UserId).Child("username").SetValueAsync(username);
            AddUserToOnline(result.User.UserId);
            currentUserId = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;

            if (currentUserId != null)
            {
                FirebaseDatabase.DefaultInstance.GetReference($"invitaciones/{currentUserId}")
                    .ValueChanged += HandleInvitationReceived;

                FirebaseDatabase.DefaultInstance.GetReference($"partidas/{currentUserId}")
                    .ValueChanged += HandlePartidaAceptada;
            }

            ActualizarDatos();
        }
    }

    private void AddUserToOnline(string userId)
    {
        FirebaseDatabase.DefaultInstance.GetReference("users/" + userId + "/username").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || !task.IsCompleted)
            {
                Debug.LogError("Error al obtener el nombre del usuario.");
                return;
            }

            DataSnapshot snapshot = task.Result;
            string username = snapshot.Value.ToString();
            mDataBaseRef.Child("online-users").Child(userId).SetValueAsync(username);
        });
    }

    private void RemoveUserFromOnline()
    {
        if (currentUserId != null)
        {
            mDataBaseRef.Child("online-users").Child(currentUserId).RemoveValueAsync();
        }
    }

    private void ActualizarDatos()
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            var userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;

            FirebaseDatabase.DefaultInstance.GetReference("users/" + userId).GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    DataSnapshot snapshot = task.Result;
                    textos[0].text = snapshot.HasChild("username") ? snapshot.Child("username").Value.ToString().ToUpper() : "Desconocido";
                    ListUsers();
                }
            });
        }
    }

    private void ListUsers()
    {
        foreach (Transform child in contenedorUsuarios)
        {
            Destroy(child.gameObject);
        }

        FirebaseDatabase.DefaultInstance.GetReference("online-users").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                DataSnapshot snapshot = task.Result;

                foreach (var item in snapshot.Children)
                {
                    string userId = item.Key;
                    string username = item.Value.ToString();

                    if (userId != currentUserId)
                    {
                        GameObject usuarioObj = Instantiate(usuarioPrefab, contenedorUsuarios);
                        TextMeshProUGUI nombreText = usuarioObj.transform.Find("NombreText").GetComponent<TextMeshProUGUI>();
                        Button invitarButton = usuarioObj.transform.Find("InvitarButton").GetComponent<Button>();

                        nombreText.text = username;
                        invitarButton.onClick.AddListener(() => EnviarInvitacion(userId, username));
                    }
                }
            }
        });
    }

    private void HandleUserConnected(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError(args.DatabaseError.Message);
            return;
        }

        string userId = args.Snapshot.Key;
        string username = args.Snapshot.Value.ToString();

        if (userId != currentUserId)
        {
            GameObject usuarioObj = Instantiate(usuarioPrefab, contenedorUsuarios);
            TextMeshProUGUI nombreText = usuarioObj.transform.Find("NombreText").GetComponent<TextMeshProUGUI>();
            Button invitarButton = usuarioObj.transform.Find("InvitarButton").GetComponent<Button>();

            nombreText.text = username;
            invitarButton.onClick.AddListener(() => EnviarInvitacion(userId, username));
        }
    }

    private void EnviarInvitacion(string userIdDestinatario, string nombreDestinatario)
    {
        string userIdEmisor = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
        string nombreEmisor = textos[0].text;

        DatabaseReference invitacionesRef = FirebaseDatabase.DefaultInstance.GetReference("invitaciones");
        invitacionesRef.Child(userIdDestinatario).SetValueAsync(new Dictionary<string, object>
        {
            { "emisorId", userIdEmisor },
            { "emisorNombre", nombreEmisor }
        });

        Debug.Log($"Invitación enviada a {nombreDestinatario}");
    }

    private void HandleInvitationReceived(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError(args.DatabaseError.Message);
            return;
        }

        if (args.Snapshot.Exists)
        {
            emisorId = args.Snapshot.Child("emisorId").Value.ToString();
            emisorNombre = args.Snapshot.Child("emisorNombre").Value.ToString();

            objetoActivar.SetActive(true);
            emisor.text = emisorNombre;

            botonAceptar.onClick.RemoveAllListeners();
            botonAceptar.onClick.AddListener(AceptarInvitacion);
        }
    }

    private void AceptarInvitacion()
    {
        if (emisorId == null) return;

        FirebaseDatabase.DefaultInstance.GetReference($"invitaciones/{currentUserId}").RemoveValueAsync();

        mDataBaseRef.Child("partidas").Child(emisorId).SetValueAsync(new Dictionary<string, object>
        {
            { "receptorId", currentUserId },
            { "receptorNombre", textos[0].text }
        });

        ConfigurarTextoPartida(textos[0].text, emisorNombre);
        Debug.Log("Invitación aceptada. Notificando al emisor.");
    }

    private void HandlePartidaAceptada(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError(args.DatabaseError.Message);
            return;
        }

        if (args.Snapshot.Exists)
        {
            string receptorNombre = args.Snapshot.Child("receptorNombre").Value.ToString();
            ConfigurarTextoPartida(textos[0].text, receptorNombre);

            FirebaseDatabase.DefaultInstance.GetReference($"partidas/{currentUserId}").RemoveValueAsync();
            Debug.Log("La invitación fue aceptada. Activando objeto de partida.");
        }
    }

    private void ConfigurarTextoPartida(string nombrePropio, string nombreOtro)
    {
        textoJugadorPropio.text = nombrePropio;
        textoJugadorOtro.text = nombreOtro;

        objetoinvitar.SetActive(false);
        objetoInicio.SetActive(false);
        objetoPartida.SetActive(true);
    }

    private void HandleUserDisconnected(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError(args.DatabaseError.Message);
            return;
        }

        string userId = args.Snapshot.Key;

        foreach (Transform child in contenedorUsuarios)
        {
            TextMeshProUGUI nombreText = child.Find("NombreText").GetComponent<TextMeshProUGUI>();
            if (nombreText != null && nombreText.text == args.Snapshot.Value.ToString())
            {
                Destroy(child.gameObject);
                break;
            }
        }
    }

    private void HandleLogOutButton()
    {
        RemoveUserFromOnline();
        FirebaseAuth.DefaultInstance.SignOut();
    }

    private void HandleResetPassword() { }
}
