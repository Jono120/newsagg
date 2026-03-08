import { useEffect, useRef } from "react";

const KOFI_SCRIPT_SRC = "https://storage.ko-fi.com/cdn/widget/Widget_2.js";

function KoFI({
    kofiId,
    text = "Support me on Ko-fi",
    color = "#72a4f2",
}) {
    const containerRef = useRef(null);

    useEffect(() => {
        const mountPoint = containerRef.current;
        if (!mountPoint || !kofiId) return;

        const renderWidget = () => {
            const widget = window.kofiwidget2;
            if (!widget) return;

            widget.init(text, color, kofiId);

            // Render the widget into this component instead of using document.write.
            if (typeof widget.getHTML === "function") {
                mountPoint.innerHTML = widget.getHTML();
            } else {
                mountPoint.innerHTML =
                    `<a class="kofi-fallback-link" href="https://ko-fi.com/${kofiId}" ` +
                    `target="_blank" rel="noreferrer">${text}</a>`;
            }
        };

        if (window.kofiwidget2) {
            renderWidget();
            return;
        }

        const existingScript = document.querySelector("script[data-kofi-widget='true']");

        if (existingScript) {
            existingScript.addEventListener("load", renderWidget);
            return () => existingScript.removeEventListener("load", renderWidget);
        }

        const script = document.createElement("script");
        script.src = KOFI_SCRIPT_SRC;
        script.async = true;
        script.dataset.kofiWidget = "true";
        script.addEventListener("load", renderWidget);
        document.body.appendChild(script);

        return () => {
            script.removeEventListener("load", renderWidget);
        };
    }, [kofiId, text, color]);

    return <div ref={containerRef} className="kofi-widget" aria-label="Ko-fi support" />;
}

export default KoFI;