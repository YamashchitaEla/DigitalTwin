import Layout from "./components/Layout";
import MainPage from "./pages/MainPage";
import { Routes, Route } from "react-router-dom";
import { useEffect, useState, useRef } from "react";
import * as signalR from "@microsoft/signalr";

export default function App() {
    const [connectionStatus, setConnectionStatus] = useState("Connecting");
    const connectionRef = useRef(null);

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("http://localhost:5122/rtime/telemetry", {
                withCredentials: true
            })
            .configureLogging(signalR.LogLevel.Critical)
            .withAutomaticReconnect()
            .build();

        connectionRef.current = connection;
        connection.onreconnecting(() => {
            setConnectionStatus("Reconnecting");
        });
        connection.onreconnected(() => {
            setConnectionStatus("Connected");
        });
        connection.onclose(() => {
            setConnectionStatus("Disconnected");
        });
        connection.start()
            .then(() => {
                setConnectionStatus("Connected");
            })
            .catch(console.error);
        return () => {
            connection.stop();
        };
    }, []);


    return (
        <Routes>
            <Route 
                path="/" 
                element={
                    <Layout connectionStatus={connectionStatus}>
                        <MainPage connection={connectionRef.current}/>
                    </Layout>
                }
            />
        </Routes>
    );
}