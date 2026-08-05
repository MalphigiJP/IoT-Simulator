import { useEffect, useState } from "react";
import api from "./services/api";

function App() {

    const [message, setMessage] = useState("");

    useEffect(() => {

        api.get("/")
            .then(res => setMessage(res.data))
            .catch(err => console.log(err));

    }, []);

    return (
        <>
            <h1>{message}</h1>
        </>
    );

}

export default App;