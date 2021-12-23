using System;
using System.Diagnostics;

namespace SuperSocket.SocketBase.Protocol
{
    public class BasicRequestInfoParser : IRequestInfoParser<StringRequsetInfo>
    {
        private readonly string m_Spliter;
        private readonly string[] m_ParameterSpliters;

        private const string m_OneSpace = " ";

        public static readonly BasicRequestInfoParser DefaultInstance = new BasicRequestInfoParser();

        public BasicRequestInfoParser() : this(m_OneSpace, m_OneSpace)
        {
            
        }

        public BasicRequestInfoParser(string spliter, string parameterSpliter)
        {
            m_Spliter = spliter;
            m_ParameterSpliters = new string[] { parameterSpliter };
        }

        #region IRequestInfoParser<StringRequstInfo> Members

        public StringRequsetInfo parseRequestInfo(string source)
        {
            int pos = source.IndexOf(m_Spliter);

            string name = string.Empty;
            string param = string.Empty;

            if (pos > 0)
            {
                name = source.Substring(0, pos);
                param = source.Substring(pos + m_Spliter.Length);
            }
            else
            {
                name = source;
            }

            return new StringRequsetInfo(name, param,
                param.Split(m_ParameterSpliters, StringSplitOptions.RemoveEmptyEntries));
        }

        #endregion
        
    }
}