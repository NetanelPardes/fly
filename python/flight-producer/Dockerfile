FROM python:3.12-slim
WORKDIR /app
COPY python/flight-producer/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY python/flight-producer/producer.py .
ENTRYPOINT ["python", "producer.py"]
