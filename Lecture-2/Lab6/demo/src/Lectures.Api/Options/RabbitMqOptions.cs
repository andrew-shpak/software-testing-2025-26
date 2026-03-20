using System.ComponentModel.DataAnnotations;

namespace Lectures.Api.Options;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string Username { get; set; } = "guest";

    [Required]
    public string Password { get; set; } = "guest";

    public Uri ToUri() => new($"amqp://{Username}:{Password}@{Host}:{Port}");
}
