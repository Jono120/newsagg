import KoFI from "./KoFI";
import BMC from "./BMC";

function SupportOptions({
  kofiId = "F1F61V9XSE",
  buyMeACoffeeSlug = "jono420",
  buyMeACoffeeUrl = "https://buymeacoffee.com/jono420",
}) {
  return (
    <div className="support-links" aria-label="Support options">
      <KoFI kofiId={kofiId} text="Support me on Ko-fi" color="#72a4f2" />
      <BMC
        slug={buyMeACoffeeSlug}
        fallbackUrl={buyMeACoffeeUrl}
        text="Buy me a coffee"
        color="#FFDD00"
        emoji=""
        font="Inter"
        outlineColor="#000000"
        fontColor="#000000"
        coffeeColor="#ffffff"
      />
    </div>
  );
}

export default SupportOptions;
