using Airport.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Airport.Api.Data;

public class AirportDbContext(DbContextOptions<AirportDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AirportLocation> AirportLocations => Set<AirportLocation>();
    public DbSet<Aircraft> Aircraft => Set<Aircraft>();
    public DbSet<Gate> Gates => Set<Gate>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<GeneratedFlightSchedule> GeneratedFlightSchedules => Set<GeneratedFlightSchedule>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Baggage> Baggage => Set<Baggage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var userRoleConverter = new EnumToStringConverter<UserRole>();
        var flightStatusConverter = new EnumToStringConverter<FlightStatus>();
        var ticketClassConverter = new EnumToStringConverter<TicketClass>();
        var ticketStatusConverter = new EnumToStringConverter<TicketStatus>();
        var baggageStatusConverter = new EnumToStringConverter<BaggageStatus>();

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Role).HasConversion(userRoleConverter).HasMaxLength(30);
            entity.Property(user => user.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.NationalId).IsUnique();
            entity.HasIndex(user => user.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Property(employee => employee.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.HasIndex(employee => employee.Email).IsUnique();
            entity.HasIndex(employee => employee.NationalId).IsUnique();
            entity.HasIndex(employee => employee.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<AirportLocation>(entity =>
        {
            entity.HasIndex(airport => new { airport.City, airport.Country });
        });

        modelBuilder.Entity<Aircraft>(entity =>
        {
            entity.HasIndex(aircraft => aircraft.Manufacturer);
            entity.HasIndex(aircraft => aircraft.Model);
        });

        modelBuilder.Entity<Gate>(entity =>
        {
            entity.HasIndex(gate => gate.GateNumber).IsUnique();
            entity.HasData(
                new Gate { Id = 1, GateNumber = "A1", Terminal = "A" },
                new Gate { Id = 2, GateNumber = "A2", Terminal = "A" },
                new Gate { Id = 3, GateNumber = "B1", Terminal = "B" });
        });

        modelBuilder.Entity<Flight>(entity =>
        {
            entity.Property(flight => flight.Status).HasConversion(flightStatusConverter).HasMaxLength(30);
            entity.Property(flight => flight.Source).HasDefaultValue("Local");
            entity.Property(flight => flight.BasePrice).HasPrecision(10, 2);
            entity.HasIndex(flight => new { flight.Origin, flight.Destination });
            entity.HasIndex(flight => new { flight.OriginAirportCode, flight.DepartureTime });
            entity.HasIndex(flight => new { flight.OriginAirportCode, flight.DestinationAirportCode, flight.DepartureTime });
            entity.HasIndex(flight => new { flight.AircraftCode, flight.DepartureTime });
            entity.HasIndex(flight => flight.Status);
            entity.HasIndex(flight => new { flight.Source, flight.ExternalId }).IsUnique();
            entity.HasOne(flight => flight.Gate)
                .WithMany(gate => gate.Flights)
                .HasForeignKey(flight => flight.GateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GeneratedFlightSchedule>(entity =>
        {
            entity.HasIndex(schedule => schedule.FlightId).IsUnique();
            entity.HasIndex(schedule => new { schedule.OriginIataCode, schedule.DestinationIataCode });
            entity.HasIndex(schedule => schedule.AirlineName);
            entity.Property(schedule => schedule.GeneratedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.HasOne(schedule => schedule.Flight)
                .WithOne(flight => flight.GeneratedSchedule)
                .HasForeignKey<GeneratedFlightSchedule>(schedule => schedule.FlightId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.Property(ticket => ticket.Class).HasConversion(ticketClassConverter).HasMaxLength(30);
            entity.Property(ticket => ticket.Status).HasConversion(ticketStatusConverter).HasMaxLength(30);
            entity.Property(ticket => ticket.BookingDate).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.Property(ticket => ticket.RouteBookingReference).HasMaxLength(40);
            entity.Property(ticket => ticket.ActiveSeatKey)
                .HasComputedColumnSql("CASE WHEN `Status` = 'Active' THEN CONCAT(`FlightId`, ':', `SeatNumber`) ELSE NULL END", stored: true);
            entity.HasIndex(ticket => ticket.ActiveSeatKey).IsUnique();
            entity.HasIndex(ticket => ticket.RouteBookingReference);
            entity.HasIndex(ticket => ticket.PassengerNationalId);
            entity.HasIndex(ticket => ticket.PassengerEmail);
            entity.HasIndex(ticket => ticket.BookingDate);
            entity.HasOne(ticket => ticket.Passenger)
                .WithMany(user => user.Tickets)
                .HasForeignKey(ticket => ticket.PassengerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.Flight)
                .WithMany(flight => flight.Tickets)
                .HasForeignKey(ticket => ticket.FlightId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Baggage>(entity =>
        {
            entity.Property(baggage => baggage.Status).HasConversion(baggageStatusConverter).HasMaxLength(40);
            entity.HasIndex(baggage => baggage.TagNumber).IsUnique();
            entity.HasIndex(baggage => baggage.TicketId).IsUnique();
            entity.HasOne(baggage => baggage.Ticket)
                .WithOne(ticket => ticket.Baggage)
                .HasForeignKey<Baggage>(baggage => baggage.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.Property(message => message.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.HasIndex(message => message.EventId).IsUnique();
            entity.HasIndex(message => new { message.PublishedAt, message.CreatedAt });
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.Property(message => message.ProcessedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.HasIndex(message => new { message.EventId, message.Consumer }).IsUnique();
        });
    }

    public override int SaveChanges()
    {
        NormalizeDates();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeDates();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void NormalizeDates()
    {
        foreach (var entry in ChangeTracker.Entries<Flight>())
        {
            entry.Entity.DepartureTime = EnsureUtc(entry.Entity.DepartureTime);
            entry.Entity.ArrivalTime = EnsureUtc(entry.Entity.ArrivalTime);

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Baggage>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.LastUpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
