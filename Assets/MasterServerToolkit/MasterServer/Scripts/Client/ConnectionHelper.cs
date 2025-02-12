﻿using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ConnectionHelper<T> : SingletonBehaviour<T> where T : MonoBehaviour
    {
        #region INSPECTOR

        [SerializeField]
        protected HelpBox header = new HelpBox()
        {
            Text = "This script connects client to server. Is is just a helper",
            Type = HelpBoxType.Info
        };

        [Header("Connection Settings"), Tooltip("Address to the server"), SerializeField]
        protected string serverIP = "127.0.0.1";

        [Tooltip("Port of the server"), SerializeField]
        protected int serverPort = 5000;

        [Header("Advanced"), SerializeField]
        protected float minTimeToConnect = 2f;
        [SerializeField]
        protected float maxTimeToConnect = 20f;
        [SerializeField]
        protected int maxAttemptsToConnect = 5;
        [SerializeField]
        protected float waitAndConnect = 0.2f;

        [Tooltip("If true, will try to connect on the Start()"), SerializeField]
        protected bool connectOnStart = false;

        [Header("Events")]
        /// <summary>
        /// Triggers when connected to server
        /// </summary>
        public UnityEvent OnConnectionOpenEvent;

        /// <summary>
        /// triggers when disconnected from server
        /// </summary>
        public UnityEvent OnConnectionCloseEvent;

        /// <summary>
        /// triggers when connection to server failed
        /// </summary>
        public UnityEvent OnConnectionFailedEvent;
        #endregion

        protected int currentAttemptToConnect = 0;
        protected float timeToConnect = 0.5f;

        /// <summary>
        /// Main connection to server
        /// </summary>
        public IClientSocket Connection { get; protected set; }

        protected override void Awake()
        {
            base.Awake();

            // If current object is destroying just make a return
            if (isNowDestroying) return;

            // Set connection if it is null
            if (Connection == null) Connection = ConnectionFactory();

            // In case this object is not at the root level of hierarchy
            // move it there, so that it won't be destroyed
            if (transform.parent != null)
            {
                transform.SetParent(null, false);
            }

            if (Mst.Args.StartClientConnection)
            {
                connectOnStart = true;
            }
        }

        protected virtual void Start()
        {
            if (connectOnStart)
            {
                StartConnection();
            }
        }

        protected virtual void OnDestroy()
        {
            Connection?.Close();
        }

        protected virtual void OnValidate()
        {
            maxAttemptsToConnect = Mathf.Clamp(maxAttemptsToConnect, 1, int.MaxValue);
            waitAndConnect = Mathf.Clamp(waitAndConnect, 0.2f, 60f);
            minTimeToConnect = Mathf.Clamp(minTimeToConnect, 5f, 60f);
            maxTimeToConnect = Mathf.Clamp(maxTimeToConnect, 5f, 60f);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual IClientSocket ConnectionFactory()
        {
            return Mst.Connection;
        }

        /// <summary>
        /// Sets the server IP
        /// </summary>
        /// <param name="serverIp"></param>
        public void SetIpAddress(string serverIp)
        {
            this.serverIP = serverIp;
        }

        /// <summary>
        /// Sets the server port
        /// </summary>
        /// <param name="masterPort"></param>
        public void SetPort(int serverPort)
        {
            this.serverPort = serverPort;
        }

        /// <summary>
        /// Starts connection to server
        /// </summary>
        public void StartConnection()
        {
            StartConnection(serverIP, serverPort, maxAttemptsToConnect);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="numberOfAttempts"></param>
        public void StartConnection(int numberOfAttempts)
        {
            StartConnection(serverIP, serverPort, numberOfAttempts);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serverIp"></param>
        /// <param name="serverPort"></param>
        /// <param name="numberOfAttempts"></param>
        public void StartConnection(string serverIp, int serverPort, int numberOfAttempts = 5)
        {
            if (gameObject && gameObject.activeSelf && gameObject.activeInHierarchy)
                StartCoroutine(StartConnectionProcess(serverIp, serverPort, numberOfAttempts));
        }

        public void StopConnectionProcess()
        {
            StopAllCoroutines();
            Connection?.Close();
        }

        protected virtual IEnumerator StartConnectionProcess(string serverIp, int serverPort, int numberOfAttempts)
        {
            currentAttemptToConnect = 0;
            maxAttemptsToConnect = numberOfAttempts > 0 ? numberOfAttempts : maxAttemptsToConnect;

            Connection.RemoveConnectionOpenListener(OnConnectedEventHandler);
            Connection.RemoveConnectionCloseListener(OnDisconnectedEventHandler);

            Connection.AddConnectionOpenListener(OnConnectedEventHandler);
            Connection.AddConnectionCloseListener(OnDisconnectedEventHandler, false);

            // Wait a fraction of a second, in case we're also starting a master server at the same time
            yield return new WaitForSeconds(waitAndConnect);

            if (!Connection.IsConnected)
            {
                logger.Info($"Starting MASTER Connection Helper... {Mst.Version}");
                logger.Info($"Connecting to MASTER server at: {serverIp}:{serverPort}");
            }

            while (true)
            {
                // If is already connected break cycle
                if (Connection.IsConnected)
                {
                    yield break;
                }

                // If currentAttemptToConnect of attempts is equals maxAttemptsToConnect stop connection
                if (currentAttemptToConnect == maxAttemptsToConnect)
                {
                    logger.Error($"Client cannot to connect to MASTER server at: {serverIp}:{serverPort}");
                    Connection.Close(false);
                    OnConnectionFailedHandler();
                    OnConnectionFailedEvent?.Invoke();
                    yield break;
                }

                // If we got here, we're not connected
                if (Connection.IsConnecting)
                {
                    if (currentAttemptToConnect > 0)
                        logger.Info($"Retrying to connect to MASTER server at: {serverIp}:{serverPort}");

                    currentAttemptToConnect++;
                }

                if (!Connection.IsConnected)
                {
                    Connection.UseSecure = MstApplicationConfig.Instance.UseSecure;
                    Connection.Connect(serverIp, serverPort);
                }

                // Give a few seconds to try and connect
                yield return new WaitForSeconds(timeToConnect);

                // If we're still not connected
                if (!Connection.IsConnected)
                {
                    timeToConnect = Mathf.Min(timeToConnect * 2, maxTimeToConnect);
                }
            }
        }

        protected virtual void OnConnectedEventHandler()
        {
            logger.Info($"Connected to MASTER server at {serverIP}:{serverPort}");
            timeToConnect = minTimeToConnect;
            OnConnectionOpenEvent?.Invoke();
        }

        protected virtual void OnConnectionFailedHandler()
        {
            logger.Info($"Connection to MASTER server at {serverIP}:{serverPort} failed");
            timeToConnect = minTimeToConnect;
            OnConnectionFailedEvent?.Invoke();
        }

        protected virtual void OnDisconnectedEventHandler()
        {
            logger.Info($"Disconnected from MASTER server");
            timeToConnect = minTimeToConnect;
            OnConnectionCloseEvent?.Invoke();
        }
    }
}