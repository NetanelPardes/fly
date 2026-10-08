import json
import os
import time
from pathlib import Path

import pika
import pymysql


def env(name, default):
    return os.getenv(name, default)


def db_connection():
    return pymysql.connect(
        host=env("MYSQL_HOST", "localhost"),
        port=int(env("MYSQL_PORT", "3306")),
        user=env("MYSQL_USER", "airport_user"),
        password=env("MYSQL_PASSWORD", "airport_password"),
        database=env("MYSQL_DATABASE", "airport_management"),
        autocommit=False,
        cursorclass=pymysql.cursors.DictCursor,
    )


def already_processed(conn, event_id):
    with conn.cursor() as cursor:
        cursor.execute(
            "SELECT Id FROM ProcessedEvents WHERE EventId = %s AND Consumer = %s",
            (event_id, "ticket-generator"),
        )
        return cursor.fetchone() is not None


def mark_processed(conn, event_id):
    with conn.cursor() as cursor:
        cursor.execute(
            "INSERT INTO ProcessedEvents (EventId, Consumer, ProcessedAt) VALUES (%s, %s, UTC_TIMESTAMP(6))",
            (event_id, "ticket-generator"),
        )


def generate_ticket_file(task):
    output_dir = Path(env("TICKET_OUTPUT_DIR", "/tickets"))
    output_dir.mkdir(parents=True, exist_ok=True)
    path = output_dir / f"ticket-{task['ticketId']}.txt"
    path.write_text(
        "\n".join(
            [
                "Airport Management System Ticket",
                f"Ticket: {task['ticketId']}",
                f"Passenger: {task['passengerName']}",
                f"Flight: {task['flightNumber']}",
                f"Route: {task['origin']} -> {task['destination']}",
                f"Departure UTC: {task['departureTime']}",
                f"Seat: {task['seatNumber']}",
                f"Class: {task['class']}",
                f"Price: {task['price']}",
                "",
            ]
        ),
        encoding="utf-8",
    )
    return str(path)


def process_task(body):
    task = json.loads(body.decode("utf-8"))
    event_id = task["eventId"]

    conn = db_connection()
    try:
        if already_processed(conn, event_id):
            conn.rollback()
            print(f"Skipped duplicate event {event_id}")
            return

        file_path = generate_ticket_file(task)
        with conn.cursor() as cursor:
            cursor.execute(
                "UPDATE Tickets SET TicketFilePath = %s WHERE Id = %s",
                (file_path, task["ticketId"]),
            )
        mark_processed(conn, event_id)
        conn.commit()
        print(f"Generated {file_path}")
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()


def main():
    credentials = pika.PlainCredentials(env("RABBITMQ_USER", "guest"), env("RABBITMQ_PASSWORD", "guest"))
    parameters = pika.ConnectionParameters(
        host=env("RABBITMQ_HOST", "localhost"),
        port=int(env("RABBITMQ_PORT", "5672")),
        credentials=credentials,
        heartbeat=30,
    )
    queue = env("RABBITMQ_TICKET_QUEUE", "ticket-generation")
    max_retries = int(env("TICKET_WORKER_MAX_RETRIES", "5"))

    while True:
        try:
            connection = pika.BlockingConnection(parameters)
            break
        except pika.exceptions.AMQPConnectionError:
            print("Waiting for RabbitMQ...")
            time.sleep(5)

    channel = connection.channel()
    channel.queue_declare(queue=queue, durable=True)
    channel.basic_qos(prefetch_count=1)

    def callback(ch, method, properties, body):
        headers = properties.headers or {}
        retry_count = int(headers.get("x-retry-count", 0))
        try:
            process_task(body)
            ch.basic_ack(method.delivery_tag)
        except Exception as exc:
            print(f"Ticket generation failed: {exc}")
            ch.basic_ack(method.delivery_tag)
            if retry_count < max_retries:
                retry_props = pika.BasicProperties(
                    delivery_mode=2,
                    message_id=properties.message_id,
                    type=properties.type,
                    headers={"x-retry-count": retry_count + 1},
                )
                ch.basic_publish(exchange="", routing_key=queue, body=body, properties=retry_props)
            else:
                print("Retry limit reached; message was dropped after bounded retries.")

    channel.basic_consume(queue=queue, on_message_callback=callback)
    print(f"Ticket worker listening on {queue}...")
    channel.start_consuming()


if __name__ == "__main__":
    main()
