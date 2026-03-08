import { useEffect, useRef } from "react";

const BMC_SCRIPT_SRC = "https://cdnjs.buymeacoffee.com/1.0.0/button.prod.min.js";

function BMC({
  slug = "jono420",
  text = "Buy me a coffee",
  color = "#FFDD00",
  emoji = "",
  font = "Inter",
  fontColor = "#000000",
  outlineColor = "#000000",
  coffeeColor = "#ffffff",
  fallbackUrl,
}) {
  const containerRef = useRef(null);

  useEffect(() => {
    const mountPoint = containerRef.current;
    if (!mountPoint) return;

    const finalSlug = slug || "jono420";
    const finalUrl = fallbackUrl || `https://buymeacoffee.com/${finalSlug}`;

    const renderWidget = () => {
      const widget = window.bmcBtnWidget;

      if (typeof widget === "function") {
        mountPoint.innerHTML = widget(
          text,
          finalSlug,
          color,
          emoji,
          font,
          fontColor,
          outlineColor,
          coffeeColor,
        );
        return;
      }

      mountPoint.innerHTML =
        `<a class="buy-me-coffee-link" href="${finalUrl}" ` +
        `target="_blank" rel="noreferrer">${text}</a>`;
    };

    if (window.bmcBtnWidget) {
      renderWidget();
      return;
    }

    const existingScript = document.querySelector("script[data-bmc-widget='true']");

    if (existingScript) {
      existingScript.addEventListener("load", renderWidget);
      return () => existingScript.removeEventListener("load", renderWidget);
    }

    const script = document.createElement("script");
    script.src = BMC_SCRIPT_SRC;
    script.async = true;
    script.dataset.bmcWidget = "true";
    script.addEventListener("load", renderWidget);
    document.body.appendChild(script);

    return () => {
      script.removeEventListener("load", renderWidget);
    };
  }, [slug, text, color, emoji, font, fontColor, outlineColor, coffeeColor, fallbackUrl]);

  return (
    <div ref={containerRef} className="bmc-widget" aria-label="Buy Me a Coffee support" />
  );
}

export default BMC;
