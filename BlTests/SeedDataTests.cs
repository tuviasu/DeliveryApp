using ProcessResult = DO.ProcessResult;

namespace BlTests;

/// <summary>
/// The demo-data generator (DalTest.Initialization) used to create independent, overlapping
/// attempts per order - e.g. two couriers both "Completed" on the same order (order 1000 in xml).
/// These checks run on the in-memory DalList only; the real xml\ files are never touched.
/// </summary>
public class SeedDataTests
{
    private static readonly ProcessResult[] Closing =
        { ProcessResult.Completed, ProcessResult.Cancelled, ProcessResult.CustomerRefused };

    [Fact]
    public void GeneratedDeliveries_AreSequential_AndOnlyLastAttemptCanCloseTheOrder()
    {
        var dal = DalApi.Factory.Get;

        for (int run = 0; run < 5; run++) // generator is random - check several runs
        {
            DalTest.Initialization.Do();

            foreach (var group in dal.Delivery.ReadAll().GroupBy(d => d.OrderID))
            {
                var attempts = group.OrderBy(d => d.DeliveryStartTime).ToList();

                Assert.All(attempts, d => Assert.NotNull(d.DeliveryDoneTime));
                Assert.True(attempts.Count(d => Closing.Contains(d.DeliveryDoneType!.Value)) <= 1,
                    $"order {group.Key} has more than one closing attempt");

                for (int i = 0; i < attempts.Count - 1; i++)
                {
                    Assert.DoesNotContain(attempts[i].DeliveryDoneType!.Value, Closing);
                    Assert.True(attempts[i].DeliveryDoneTime <= attempts[i + 1].DeliveryStartTime,
                        $"order {group.Key} has overlapping attempts");
                }
            }
        }

        dal.ResetDB();
    }
}
