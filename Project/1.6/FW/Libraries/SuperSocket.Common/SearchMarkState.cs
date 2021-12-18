﻿using System;

namespace SuperSocket.Common
{
    /// <summary>
    /// SearchMarkState
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SearchMarkState<T> where T : IEquatable<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SearchMarkState&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="mark">the mark</param>
        public SearchMarkState(T[] mark)
        {
            Mark = mark;
        }

        /// <summary>
        /// Gets the Mark
        /// </summary>
        public T[] Mark { get; private set; }

        /// <summary>
        /// Gets or Sets whether matched already,
        /// </summary>
        /// <value>
        /// The Matched.
        /// </value>
        public int Matched
        {
            get;
            set;
        }
    }
}