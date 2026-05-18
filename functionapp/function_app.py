import logging
import os

import azure.functions as func
import requests


app = func.FunctionApp(http_auth_level=func.AuthLevel.FUNCTION)


def trigger_scrape() -> str:
    refresh_url = os.getenv("SCRAPER_REFRESH_URL", "http://backend:8080/api/scraper/refresh")
    response = requests.post(refresh_url, timeout=60)
    response.raise_for_status()
    return response.text or "Scraper refresh triggered"


@app.function_name(name="refresh_articles_timer")
@app.timer_trigger(schedule="%SCRAPE_SCHEDULE_CRON%", arg_name="timer", run_on_startup=False, use_monitor=True)
def refresh_articles_timer(timer: func.TimerRequest) -> None:
    logging.info("Timer trigger fired; requesting backend scraper refresh")
    try:
        trigger_scrape()
    except Exception:
        logging.exception("Timed scraper refresh failed")


@app.function_name(name="refresh_articles_http")
@app.route(route="scraper/refresh", methods=["POST"], auth_level=func.AuthLevel.FUNCTION)
def refresh_articles_http(req: func.HttpRequest) -> func.HttpResponse:
    try:
        result = trigger_scrape()
        return func.HttpResponse(result, status_code=200)
    except Exception as exc:
        logging.exception("HTTP scraper refresh failed")
        return func.HttpResponse(str(exc), status_code=500)