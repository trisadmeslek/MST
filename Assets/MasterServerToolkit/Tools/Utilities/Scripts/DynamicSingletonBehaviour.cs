﻿using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public class DynamicSingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// Check if this object is not currently being destroyed
        /// </summary>
        protected bool isNowDestroying = false;
        /// <summary>
        /// Current instance of this singleton
        /// </summary>
        private static T _instance { get; set; }

        protected virtual void Awake()
        {
            StartCoroutine(WaitAndDestroy());
        }

        private IEnumerator WaitAndDestroy()
        {
            yield return new WaitForEndOfFrame();

            if (_instance != null && _instance != this)
            {
                isNowDestroying = true;
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Current instance of this singleton
        /// </summary>
        public static T Instance
        {
            get
            {
                if (!_instance)
                    Create();

                return _instance;
            }
        }

        /// <summary>
        /// Tries to create new instance of this singleton
        /// </summary>
        public static void Create()
        {
            Create(string.Empty);
        }

        /// <summary>
        /// Tries to create new instance of this singleton with new given name
        /// </summary>
        /// <param name="name"></param>
        public static void Create(string name)
        {
            if (_instance)
                return;

            _instance = FindObjectOfType<T>();

            if (_instance)
                return;

            string newName = !string.IsNullOrEmpty(name) ? name : $"--{typeof(T).Name}".ToUpper();
            var go = new GameObject(newName);
            _instance = go.AddComponent<T>();
            DontDestroyOnLoad(_instance);
        }
    }
}
