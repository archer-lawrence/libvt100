using System;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace libVT100
{
    public class VT100Decoder : EscapeCharacterDecoder, IVT100Decoder
    {
        protected List<IVT100DecoderClient> m_listeners;

        Encoding IDecoder.Encoding
        {
            get
            {
                return m_encoding;
            }
            set
            {
                if ( m_encoding != value )
                {
                    m_encoding = value;
                    m_decoder = m_encoding.GetDecoder();
                    m_encoder = m_encoding.GetEncoder();
                }
            }
        }

        public VT100Decoder()
            : base()
        {
            m_listeners = new List<IVT100DecoderClient>();
        }

        private int DecodeInt( String _value, int _default )
        {
            if ( _value.Length == 0 )
            {
                return _default;
            }
            int ret;
            if ( Int32.TryParse( _value.TrimStart( '0' ), out ret ) )
            {
                return ret;
            }
            else
            {
                return _default;
            }
        }

        protected override void ProcessCommand( byte _command, String _parameter )
        {
            //System.Console.WriteLine ( "ProcessCommand: {0} {1}", (char) _command, _parameter );
            switch ( (char) _command )
            {
                case 'A':
                    OnMoveCursor( Direction.Up, DecodeInt( _parameter, 1 ) );
                    break;

                case 'B':
                    OnMoveCursor( Direction.Down, DecodeInt( _parameter, 1 ) );
                    break;

                case 'C':
                    OnMoveCursor( Direction.Forward, DecodeInt( _parameter, 1 ) );
                    break;

                case 'D':
                    OnMoveCursor( Direction.Backward, DecodeInt( _parameter, 1 ) );
                    break;

                case 'E':
                    OnMoveCursorToBeginningOfLineBelow( DecodeInt( _parameter, 1 ) );
                    break;

                case 'F':
                    OnMoveCursorToBeginningOfLineAbove( DecodeInt( _parameter, 1 ) );
                    break;

                case 'G':
                    OnMoveCursorToColumn( DecodeInt( _parameter, 1 ) - 1 );
                    break;

                case 'H':
                case 'f':
                    {
                        int separator = _parameter.IndexOf( ';' );
                        if ( separator == -1 )
                        {
                            OnMoveCursorTo( new Point( 0, 0 ) );
                        }
                        else
                        {
                            String row = _parameter.Substring( 0, separator );
                            String column = _parameter.Substring( separator + 1, _parameter.Length - separator - 1 );
                            OnMoveCursorTo( new Point( DecodeInt( row, 1 ) - 1, DecodeInt( column, 1 ) - 1 ) );
                        }
                    }
                    break;

                case 'J':
                    OnClearScreen( (ClearDirection) DecodeInt( _parameter, 0 ) );
                    break;

                case 'K':
                    OnClearLine( (ClearDirection) DecodeInt( _parameter, 0 ) );
                    break;

                case 'S':
                    OnScrollPageUpwards( DecodeInt( _parameter, 1 ) );
                    break;

                case 'T':
                    OnScrollPageDownwards( DecodeInt( _parameter, 1 ) );
                    break;

                case 'm':
                    {
                        String[] commands = _parameter.Split( ';' );
                        GraphicRendition[] renditionCommands = new GraphicRendition[commands.Length];
                        for ( int i = 0 ; i < commands.Length ; ++i )
                        {
                            renditionCommands[i] = (GraphicRendition) DecodeInt( commands[i], 0 );
                            //System.Console.WriteLine ( "Rendition command: {0} = {1}", commands[i], renditionCommands[i]);
                        }
                        OnSetGraphicRendition( renditionCommands );
                    }
                    break;

                case 'n':
                    if ( _parameter == "6" )
                    {
                        Point cursorPosition = OnGetCursorPosition();
                        cursorPosition.X++;
                        cursorPosition.Y++;
                        String row = cursorPosition.Y.ToString();
                        String column = cursorPosition.X.ToString();
                        byte[] output = new byte[2 + row.Length + 1 + column.Length + 1];
                        int i = 0;
                        output[i++] = EscapeCharacter;
                        output[i++] = LeftBracketCharacter;
                        foreach ( char c in row )
                        {
                            output[i++] = (byte) c;
                        }
                        output[i++] = (byte) ';';
                        foreach ( char c in column )
                        {
                            output[i++] = (byte) c;
                        }
                        output[i++] = (byte) 'R';
                        OnOutput( output );
                    }
                    break;

                case 's':
                    OnSaveCursor();
                    break;

                case 'u':
                    OnRestoreCursor();
                    break;

                case 'l':
                    if ( _parameter == "?25" )
                    {
                        OnHideCursor();
                    }
                    else
                    {
                        throw new Exception( String.Format( "Unknown parameter for l command: {0}", _parameter ) );
                    }
                    break;

                case 'h':
                    if ( _parameter == "?25" )
                    {
                        OnShowCursor();
                    }
                    else
                    {
                        throw new Exception( String.Format( "Unknown parameter for l command: {0}", _parameter ) );
                    }
                    break;

                default:
                    throw new Exception( String.Format( "Unknown command: {0} ({1:X2}), {2}", (char) _command, _command, _parameter ) );
            }
        }

        protected virtual void OnSetGraphicRendition( GraphicRendition[] _commands )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.SetGraphicRendition( this, _commands );
            }
        }

        protected virtual void OnScrollPageUpwards( int _linesToScroll )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.ScrollPageUpwards( this, _linesToScroll );
            }
        }

        protected virtual void OnScrollPageDownwards( int _linesToScroll )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.ScrollPageDownwards( this, _linesToScroll );
            }
        }

        protected virtual void OnHideCursor()
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.HideCursor( this );
            }
        }

        protected virtual void OnShowCursor()
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.ShowCursor( this );
            }
        }

        protected virtual void OnSaveCursor()
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.SaveCursor( this );
            }
        }

        protected virtual void OnRestoreCursor()
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.RestoreCursor( this );
            }
        }

        protected virtual Point OnGetCursorPosition()
        {
            Point ret;
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                ret = client.GetCursorPosition( this );
                if ( !ret.IsEmpty )
                {
                    return ret;
                }
            }
            return Point.Empty;
        }

        protected virtual void OnClearScreen( ClearDirection _direction )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.ClearScreen( this, _direction );
            }
        }

        protected virtual void OnClearLine( ClearDirection _direction )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.ClearLine( this, _direction );
            }
        }

        protected virtual void OnMoveCursorTo( Point _position )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.MoveCursorTo( this, _position );
            }
        }

        protected virtual void OnMoveCursorToColumn( int _columnNumber )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.MoveCursorToColumn( this, _columnNumber );
            }
        }

        protected virtual void OnMoveCursor( Direction _direction, int _amount )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.MoveCursor( this, _direction, _amount );
            }
        }

        protected virtual void OnMoveCursorToBeginningOfLineBelow( int _lineNumberRelativeToCurrentLine )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.MoveCursorToBeginningOfLineBelow( this, _lineNumberRelativeToCurrentLine );
            }
        }

        protected virtual void OnMoveCursorToBeginningOfLineAbove( int _lineNumberRelativeToCurrentLine )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.MoveCursorToBeginningOfLineAbove( this, _lineNumberRelativeToCurrentLine );
            }
        }

        protected override void OnCharacters( char[] _characters )
        {
            foreach ( IVT100DecoderClient client in m_listeners )
            {
                client.Characters( this, _characters );
            }
        }

        protected override void OnOutput( byte[] _output )
        {
            if ( Output != null )
            {
                Output( this, _output );
            }
        }

        private static string[] FUNCTIONKEY_MAP = { 
        //      F1     F2     F3     F4     F5     F6     F7     F8     F9     F10    F11  F12
            "11", "12", "13", "14", "15", "17", "18", "19", "20", "21", "23", "24",
        //      F13    F14    F15    F16    F17  F18    F19    F20    F21    F22
            "25", "26", "28", "29", "31", "32", "33", "34", "23", "24" };
        
        void IDecoder.KeyPressed( Keys _modifiers, Keys _key )
        {
            if ( (int) Keys.F1 <= (int) _key && (int) _key <= (int) Keys.F12 )
            {
                byte[] r = new byte[5];
                r[0] = 0x1B;
                r[1] = (byte) '[';
                int n = (int) _key - (int) Keys.F1;
                if ( (_modifiers & Keys.Shift) != Keys.None )
                    n += 10;
                char tail;
                if ( n >= 20 )
                    tail = (_modifiers & Keys.Control) != Keys.None ? '@' : '$';
                else
                    tail = (_modifiers & Keys.Control) != Keys.None ? '^' : '~';
                string f = FUNCTIONKEY_MAP[n];
                r[2] = (byte) f[0];
                r[3] = (byte) f[1];
                r[4] = (byte) tail;
                OnOutput( r );
            }
            else if ( _key == Keys.Left || _key == Keys.Right || _key == Keys.Up || _key == Keys.Down )
            {
                byte[] r = new byte[3];
                r[0] = 0x1B;
                //if ( _cursorKeyMode == TerminalMode.Normal )
                    r[1] = (byte) '[';
                //else
                //    r[1] = (byte) 'O';

                switch ( _key )
                {
                    case Keys.Up:
                        r[2] = (byte) 'A';
                        break;
                    case Keys.Down:
                        r[2] = (byte) 'B';
                        break;
                    case Keys.Right:
                        r[2] = (byte) 'C';
                        break;
                    case Keys.Left:
                        r[2] = (byte) 'D';
                        break;
                    default:
                        throw new ArgumentException( "unknown cursor key code", "key" );
                }
                OnOutput( r );
            }
            else
            {
                byte[] r = new byte[4];
                r[0] = 0x1B;
                r[1] = (byte) '[';
                r[3] = (byte) '~';
                if ( _key == Keys.Insert )
                {
                    r[2] = (byte) '1';
                }
                else if ( _key == Keys.Home )
                {
                    r[2] = (byte) '2';
                }
                else if ( _key == Keys.PageUp )
                {
                    r[2] = (byte) '3';
                }
                else if ( _key == Keys.Delete )
                {
                    r[2] = (byte) '4';
                }
                else if ( _key == Keys.End )
                {
                    r[2] = (byte) '5';
                }
                else if ( _key == Keys.PageDown )
                {
                    r[2] = (byte) '6';
                }
                else if ( _key == Keys.Enter )
                {
                    //return new byte[] { 0x1B, (byte) 'M', (byte) '~' };
                    //r[1] = (byte) 'O';
                    //r[2] = (byte) 'M';
                    //return new byte[] { (byte) '\r', (byte) '\n' };
                    r = new byte[] { 13 };
                }
                else if ( _key == Keys.Escape )
                {
                    r = new byte[] { (byte) 0x1B };
                }
                else
                {
                    return;
                }
                OnOutput( r );
            }
        }

        public override event DecoderOutputDelegate Output;

        void IVT100Decoder.Subscribe( IVT100DecoderClient _client )
        {
            m_listeners.Add( _client );
        }

        void IVT100Decoder.UnSubscribe( IVT100DecoderClient _client )
        {
            m_listeners.Remove( _client );
        }

        void IDisposable.Dispose()
        {
            m_listeners.Clear();
            m_listeners = null;
        }
    }
}
