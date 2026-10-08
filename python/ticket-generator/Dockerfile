FROM python:3.12-slim
WORKDIR /app
COPY python/ticket-generator/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY python/ticket-generator/worker.py .
RUN mkdir -p /tickets
ENTRYPOINT ["python", "worker.py"]
