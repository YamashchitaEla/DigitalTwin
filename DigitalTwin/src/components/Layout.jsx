import Footer from "./Footer";
import Header from "./Header";

// Layout component
export default function Layout({ children, connectionStatus }) {
    return (
    <>
        <Header connectionStatus={connectionStatus}/>
        <main>
            {children}
        </main>
        <Footer />
    </>
    );
}