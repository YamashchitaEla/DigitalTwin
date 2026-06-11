import '../styles/_header.css';

export default function Header({ connectionStatus }) {
    const getConnectionClass = () => {
        switch(connectionStatus){
            case "Connected":
                return "connection_connected";
            case "Reconnecting":
                return "connection_reconnecting";
            default:
                return "connection_disconnected";
        }
    };

    return (
        <header className="header_container">
            <div className="header-info">
                <h1 className="header-title">
                    Цифровий Двійник | Digital Twin
                </h1>
                <p className="header-subtitle">
                    IoT-Моніторинг & Предиктивне обслуговування обладнання
                </p>
            </div>
            <div className={getConnectionClass()}>
                SignalR: {connectionStatus}
            </div>
        </header>
    );
}