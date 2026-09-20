using System;
using UnityEngine;

namespace Unity.FPS.AvatarBoss.Configs
{
    public abstract class ConfigContainer<T> : ConfigContainerBase where T : ScriptableObject
    {
        [SerializeField, HideInInspector]
        protected T[] _elements;

        public T[] Elements => _elements;
        public override Type ElementType => typeof(T);

        public override ScriptableObject[] GetElementsForEditor()
        {
            if (_elements == null)
                return Array.Empty<ScriptableObject>();

            var result = new ScriptableObject[_elements.Length];
            for (var i = 0; i < _elements.Length; i++)
                result[i] = _elements[i];
            return result;
        }

        public override void SetElementsForEditor(ScriptableObject[] elements)
        {
            if (elements == null)
            {
                _elements = Array.Empty<T>();
                return;
            }

            var result = new T[elements.Length];
            for (var i = 0; i < elements.Length; i++)
                result[i] = elements[i] as T;
            _elements = result;
        }
    }
}
